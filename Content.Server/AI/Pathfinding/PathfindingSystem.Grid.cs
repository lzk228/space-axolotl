using Content.Server.Access;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Utility;

namespace Content.Server.AI.Pathfinding;

/// <summary>
/// Handles pathfinding while on a grid.
/// </summary>
public sealed partial class PathfindingSystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    // Queued pathfinding graph updates
    private readonly Queue<CollisionChangeMessage> _collidableUpdateQueue = new();
    private readonly Queue<MoveEvent> _moveUpdateQueue = new();
    private readonly Queue<AccessReaderChangeEvent> _accessReaderUpdateQueue = new();
    private readonly Queue<TileRef> _tileUpdateQueue = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(Reset);
        SubscribeLocalEvent<CollisionChangeMessage>(QueueCollisionChangeMessage);
        SubscribeLocalEvent<MoveEvent>(QueueMoveEvent);
        SubscribeLocalEvent<AccessReaderChangeEvent>(QueueAccessChangeMessage);
        SubscribeLocalEvent<GridAddEvent>(OnGridAdd);
        SubscribeLocalEvent<TileChangedEvent>(QueueTileChange);

        // Handle all the base grid changes
        // Anything that affects traversal (i.e. collision layer) is handled separately.
    }

    private void OnGridAdd(GridAddEvent ev)
    {
        EnsureComp<GridPathfindingComponent>(ev.EntityUid);
    }

    private void QueueCollisionChangeMessage(CollisionChangeMessage collisionMessage)
    {
        _collidableUpdateQueue.Enqueue(collisionMessage);
    }

    private void QueueMoveEvent(ref MoveEvent moveEvent)
    {
        _moveUpdateQueue.Enqueue(moveEvent);
    }

    private void QueueTileChange(TileChangedEvent ev)
    {
        _tileUpdateQueue.Enqueue(ev.NewTile);
    }

    private void QueueAccessChangeMessage(AccessReaderChangeEvent message)
    {
        _accessReaderUpdateQueue.Enqueue(message);
    }

    private PathfindingChunk GetOrCreateChunk(TileRef tile)
    {
        var chunkX = (int) (Math.Floor((float) tile.X / PathfindingChunk.ChunkSize) * PathfindingChunk.ChunkSize);
        var chunkY = (int) (Math.Floor((float) tile.Y / PathfindingChunk.ChunkSize) * PathfindingChunk.ChunkSize);
        var vector2i = new Vector2i(chunkX, chunkY);
        var comp = Comp<GridPathfindingComponent>(tile.GridIndex);
        var chunks = comp.Graph;

        if (!chunks.TryGetValue(vector2i, out var chunk))
        {
            chunk = CreateChunk(comp, vector2i);
        }

        return chunk;
    }

    private PathfindingChunk CreateChunk(GridPathfindingComponent comp, Vector2i indices)
    {
        var grid = _mapManager.GetGrid(comp.Owner);
        var newChunk = new PathfindingChunk(grid.Index, indices);
        comp.Graph.Add(indices, newChunk);
        newChunk.Initialize(grid);

        return newChunk;
    }

    /// <summary>
    /// Return the corresponding PathfindingNode for this tile
    /// </summary>
    /// <param name="tile"></param>
    /// <returns></returns>
    public PathfindingNode GetNode(TileRef tile)
    {
        var chunk = GetOrCreateChunk(tile);
        var node = chunk.GetNode(tile);

        return node;
    }

    private void OnTileUpdate(TileRef tile)
    {
        if (!_mapManager.GridExists(tile.GridIndex)) return;

        var node = GetNode(tile);
        node.UpdateTile(tile);
    }

    private bool IsRelevant(TransformComponent xform, PhysicsComponent physics)
    {
        return xform.GridID != GridId.Invalid && (PathfindingSystem.TrackedCollisionLayers & physics.CollisionLayer) != 0;
    }

    /// <summary>
    /// Tries to add the entity to the relevant pathfinding node
    /// </summary>
    /// The node will filter it to the correct category (if possible)
    /// <param name="entity"></param>
    private void HandleEntityAdd(EntityUid entity)
    {
        if (!TryComp<TransformComponent>(entity, out var xform) ||
            !EntityManager.TryGetComponent(entity, out PhysicsComponent? physics) ||
            !IsRelevant(xform, physics) ||
            !_mapManager.TryGetGrid(xform.GridID, out var grid))
        {
            return;
        }

        var tileRef = grid.GetTileRef(xform.Coordinates);

        var chunk = GetOrCreateChunk(tileRef);
        var node = chunk.GetNode(tileRef);
        node.AddEntity(entity, physics);
    }

    private void OnEntityRemove(EntityUid entity)
    {
        var xform = Transform(entity);

        if (!_mapManager.TryGetGrid(xform.GridID, out var grid)) return;

        var node = GetNode(grid.GetTileRef(xform.Coordinates));
        node.RemoveEntity(entity);
    }

    /// <summary>
    /// When an entity moves around we'll remove it from its old node and add it to its new node (if applicable)
    /// </summary>
    /// <param name="moveEvent"></param>
    private void HandleEntityMove(MoveEvent moveEvent)
    {
        // If we've moved to space or the likes then remove us.
        if (!TryComp<TransformComponent>(moveEvent.Sender, out var xform) ||
            !TryComp<PhysicsComponent>(moveEvent.Sender, out var physics) ||
            !IsRelevant(xform, physics) ||
            moveEvent.NewPosition.GetGridId(EntityManager) == GridId.Invalid)
        {
            OnEntityRemove(moveEvent.Sender);
            return;
        }

        var oldGridId = moveEvent.OldPosition.GetGridId(EntityManager);
        var gridId = moveEvent.NewPosition.GetGridId(EntityManager);

        if (_mapManager.TryGetGrid(oldGridId, out var oldGrid))
        {
            var oldNode = GetNode(oldGrid.GetTileRef(moveEvent.OldPosition));
            oldNode.RemoveEntity(moveEvent.Sender);
        }

        if (_mapManager.TryGetGrid(gridId, out var grid))
        {
            var newNode = GetNode(grid.GetTileRef(moveEvent.OldPosition));
            newNode.AddEntity(moveEvent.Sender, physics);
        }
    }

    // TODO: Need to rethink the pathfinder utils (traversable etc.). Maybe just chuck them all in PathfindingSystem
    // Otherwise you get the steerer using this and the pathfinders using a different traversable.
    // Also look at increasing tile cost the more physics entities are on it
    public bool CanTraverse(EntityUid entity, EntityCoordinates coordinates)
    {
        var gridId = coordinates.GetGridId(EntityManager);
        var tile = _mapManager.GetGrid(gridId).GetTileRef(coordinates);
        var node = GetNode(tile);
        return CanTraverse(entity, node);
    }

    private bool CanTraverse(EntityUid entity, PathfindingNode node)
    {
        if (EntityManager.TryGetComponent(entity, out IPhysBody? physics) &&
            (physics.CollisionMask & node.BlockedCollisionMask) != 0)
        {
            return false;
        }

        var access = _accessReader.FindAccessTags(entity);
        foreach (var reader in node.AccessReaders)
        {
            if (!_accessReader.IsAllowed(reader, access))
            {
                return false;
            }
        }

        return true;
    }

    public void Reset(RoundRestartCleanupEvent ev)
    {
        _collidableUpdateQueue.Clear();
        _moveUpdateQueue.Clear();
        _accessReaderUpdateQueue.Clear();
        _tileUpdateQueue.Clear();
    }

    private void ProcessGridUpdates()
    {
        var totalUpdates = 0;

        foreach (var update in _collidableUpdateQueue)
        {
            if (Deleted(update.Owner)) continue;

            if (update.CanCollide)
            {
                HandleEntityAdd(update.Owner);
            }
            else
            {
                OnEntityRemove(update.Owner);
            }

            totalUpdates++;
        }

        _collidableUpdateQueue.Clear();

        foreach (var update in _accessReaderUpdateQueue)
        {
            if (update.Enabled)
            {
                HandleEntityAdd(update.Sender);
            }
            else
            {
                OnEntityRemove(update.Sender);
            }

            totalUpdates++;
        }

        _accessReaderUpdateQueue.Clear();

        foreach (var tile in _tileUpdateQueue)
        {
            OnTileUpdate(tile);
            totalUpdates++;
        }

        _tileUpdateQueue.Clear();
        var moveUpdateCount = Math.Max(50 - totalUpdates, 0);

        // Other updates are high priority so for this we'll just defer it if there's a spike (explosion, etc.)
        // If the move updates grow too large then we'll just do it
        if (_moveUpdateQueue.Count > 100)
        {
            moveUpdateCount = _moveUpdateQueue.Count - 100;
        }

        moveUpdateCount = Math.Min(moveUpdateCount, _moveUpdateQueue.Count);

        for (var i = 0; i < moveUpdateCount; i++)
        {
            HandleEntityMove(_moveUpdateQueue.Dequeue());
        }

        DebugTools.Assert(_moveUpdateQueue.Count < 1000);
    }
}
