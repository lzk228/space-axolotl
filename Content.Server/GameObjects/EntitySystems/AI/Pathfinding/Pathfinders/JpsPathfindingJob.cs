using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Content.Server.GameObjects.EntitySystems.JobQueues;
using Content.Server.GameObjects.EntitySystems.Pathfinding;
using Content.Shared.Pathfinding;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.EntitySystems.AI.Pathfinding.Pathfinders
{
    public class JpsPathfindingJob : Job<Queue<TileRef>>
    {
        public static event Action<JpsRouteDebug> DebugRoute;

        private PathfindingNode _startNode;
        private PathfindingNode _endNode;
        private PathfindingArgs _pathfindingArgs;
        private CancellationToken _cancellationToken;

        public JpsPathfindingJob(double maxTime,
            PathfindingNode startNode,
            PathfindingNode endNode,
            PathfindingArgs pathfindingArgs,
            CancellationToken cancellationToken) : base(maxTime)
        {
            _startNode = startNode;
            _endNode = endNode;
            _pathfindingArgs = pathfindingArgs;
            _cancellationToken = cancellationToken;
        }

        public override IEnumerator Process()
        {
            // VERY similar to A*; main difference is with the neighbor tiles you look for jump nodes instead
            if (_cancellationToken.IsCancellationRequested ||
                _startNode == null ||
                _endNode == null)
            {
                Finish();
                yield break;
            }

            // If we couldn't get a nearby node that's good enough
            if (!Utils.TryEndNode(ref _endNode, _pathfindingArgs))
            {
                Finish();
                yield break;
            }

            var openTiles = new PriorityQueue<ValueTuple<float, PathfindingNode>>(new PathfindingComparer());
            var gScores = new Dictionary<PathfindingNode, float>();
            var cameFrom = new Dictionary<PathfindingNode, PathfindingNode>();
            var closedTiles = new HashSet<PathfindingNode>();

#if DEBUG
            var jumpNodes = new HashSet<PathfindingNode>();
#endif

            PathfindingNode currentNode = null;
            openTiles.Add((0, _startNode));
            gScores[_startNode] = 0.0f;
            var routeFound = false;
            var count = 0;

            while (openTiles.Count > 0)
            {
                count++;

                // JPS probably getting a lot fewer nodes than A* is
                if (count % 5 == 0 && count > 0)
                {
                    if (OutOfTime())
                    {
                        yield return null;
                        if (_cancellationToken.IsCancellationRequested)
                        {
                            Finish();
                            yield break;
                        }
                        StopWatch.Restart();
                        Status = Status.Running;
                    }
                }

                (_, currentNode) = openTiles.Take();
                if (currentNode.Equals(_endNode))
                {
                    routeFound = true;
                    break;
                }

                foreach (var (direction, _) in currentNode.Neighbors)
                {
                    var jumpNode = GetJumpPoint(currentNode, direction, _endNode);

                    if (jumpNode != null && !closedTiles.Contains(jumpNode))
                    {
                        closedTiles.Add(jumpNode);
#if DEBUG
                        jumpNodes.Add(jumpNode);
#endif
                        // GetJumpPoint should already check if we can traverse to the node
                        var tileCost = Utils.GetTileCost(_pathfindingArgs, currentNode, jumpNode);

                        if (tileCost == null)
                        {
                            throw new InvalidOperationException();
                        }

                        var gScore = gScores[currentNode] + tileCost.Value;

                        if (gScores.TryGetValue(jumpNode, out var nextValue) && gScore >= nextValue)
                        {
                            continue;
                        }

                        cameFrom[jumpNode] = currentNode;
                        gScores[jumpNode] = gScore;
                        // pFactor is tie-breaker where the fscore is otherwise equal.
                        // See http://theory.stanford.edu/~amitp/GameProgramming/Heuristics.html#breaking-ties
                        // There's other ways to do it but future consideration
                        var fScore = gScores[jumpNode] + Utils.OctileDistance(_endNode, jumpNode) * (1.0f + 1.0f / 1000.0f);
                        openTiles.Add((fScore, jumpNode));
                    }
                }
            }

            if (!routeFound)
            {
                Finish();
                yield break;
            }

            var route = Utils.ReconstructJumpPath(cameFrom, currentNode);
            if (route.Count == 1)
            {
                Finish();
                yield break;
            }

            Finish();

#if DEBUG
            // Need to get data into an easier format to send to the relevant clients
            if (DebugRoute != null && route.Count > 0)
            {
                var debugJumpNodes = new HashSet<TileRef>(jumpNodes.Count);

                foreach (var node in jumpNodes)
                {
                    debugJumpNodes.Add(node.TileRef);
                }

                var debugRoute = new JpsRouteDebug(
                    _pathfindingArgs.Uid,
                    route,
                    debugJumpNodes,
                    DebugTime);

                DebugRoute.Invoke(debugRoute);
            }
#endif

            Result = route;
        }

        private PathfindingNode GetJumpPoint(PathfindingNode currentNode, Direction direction, PathfindingNode endNode)
        {
            var count = 0;

            while (count < 1000)
            {
                count++;
                var nextNode = currentNode.GetNeighbor(direction);

                // We'll do opposite DirectionTraversable just because of how the method's setup
                // Nodes should be 2-way anyway.
                if (nextNode == null ||
                    Utils.GetTileCost(_pathfindingArgs, currentNode, nextNode) == null)
                {
                    return null;
                }

                if (nextNode == endNode)
                {
                    return endNode;
                }

                // Horizontal and vertical are treated the same i.e.
                // They only check in their specific direction
                // (So Going North means you check NorthWest and NorthEast to see if we're a jump point)

                // Diagonals also check the cardinal directions at the same time at the same time

                // See https://harablog.wordpress.com/2011/09/07/jump-point-search/ for original description
                switch (direction)
                {
                    case Direction.East:
                        if (IsCardinalJumpPoint(direction, nextNode))
                        {
                            return nextNode;
                        }

                        break;
                    case Direction.NorthEast:
                        if (IsDiagonalJumpPoint(direction, nextNode))
                        {
                            return nextNode;
                        }

                        if (GetJumpPoint(nextNode, Direction.North, endNode) != null || GetJumpPoint(nextNode, Direction.East, endNode) != null)
                        {
                            return nextNode;
                        }

                        break;
                    case Direction.North:
                        if (IsCardinalJumpPoint(direction, nextNode))
                        {
                            return nextNode;
                        }

                        break;
                    case Direction.NorthWest:
                        if (IsDiagonalJumpPoint(direction, nextNode))
                        {
                            return nextNode;
                        }

                        if (GetJumpPoint(nextNode, Direction.North, endNode) != null || GetJumpPoint(nextNode, Direction.West, endNode) != null)
                        {
                            return nextNode;
                        }

                        break;
                    case Direction.West:
                        if (IsCardinalJumpPoint(direction, nextNode))
                        {
                            return nextNode;
                        }

                        break;
                    case Direction.SouthWest:
                        if (IsDiagonalJumpPoint(direction, nextNode))
                        {
                            return nextNode;
                        }

                        if (GetJumpPoint(nextNode, Direction.South, endNode) != null || GetJumpPoint(nextNode, Direction.West, endNode) != null)
                        {
                            return nextNode;
                        }

                        break;
                    case Direction.South:
                        if (IsCardinalJumpPoint(direction, nextNode))
                        {
                            return nextNode;
                        }

                        break;
                    case Direction.SouthEast:
                        if (IsDiagonalJumpPoint(direction, nextNode))
                        {
                            return nextNode;
                        }

                        if (GetJumpPoint(nextNode, Direction.South, endNode) != null || GetJumpPoint(nextNode, Direction.East, endNode) != null)
                        {
                            return nextNode;
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                }

                currentNode = nextNode;
            }

            Logger.WarningS("pathfinding", "Recursion found in JPS pathfinder");
            return null;
        }

        private bool IsDiagonalJumpPoint(Direction direction, PathfindingNode currentNode)
        {
            // If we're going diagonally need to check all cardinals.
            // I just just using casts int casts and offset to make it smaller but brain no workyand it wasn't working.
            // From NorthEast we check (Closed / Open) S - SE, W - NW

            PathfindingNode openNeighborOne;
            PathfindingNode closedNeighborOne;
            PathfindingNode openNeighborTwo;
            PathfindingNode closedNeighborTwo;

            switch (direction)
            {
                case Direction.NorthEast:
                    openNeighborOne = currentNode.GetNeighbor(Direction.SouthEast);
                    closedNeighborOne = currentNode.GetNeighbor(Direction.South);

                    openNeighborTwo = currentNode.GetNeighbor(Direction.NorthWest);
                    closedNeighborTwo = currentNode.GetNeighbor(Direction.West);
                    break;
                case Direction.SouthEast:
                    openNeighborOne = currentNode.GetNeighbor(Direction.NorthEast);
                    closedNeighborOne = currentNode.GetNeighbor(Direction.North);

                    openNeighborTwo = currentNode.GetNeighbor(Direction.SouthWest);
                    closedNeighborTwo = currentNode.GetNeighbor(Direction.West);
                    break;
                case Direction.SouthWest:
                    openNeighborOne = currentNode.GetNeighbor(Direction.NorthWest);
                    closedNeighborOne = currentNode.GetNeighbor(Direction.North);

                    openNeighborTwo = currentNode.GetNeighbor(Direction.SouthEast);
                    closedNeighborTwo = currentNode.GetNeighbor(Direction.East);
                    break;
                case Direction.NorthWest:
                    openNeighborOne = currentNode.GetNeighbor(Direction.SouthWest);
                    closedNeighborOne = currentNode.GetNeighbor(Direction.South);

                    openNeighborTwo = currentNode.GetNeighbor(Direction.NorthEast);
                    closedNeighborTwo = currentNode.GetNeighbor(Direction.East);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if ((closedNeighborOne == null || Utils.GetTileCost(_pathfindingArgs, currentNode, closedNeighborOne) == null)
                && openNeighborOne != null && Utils.GetTileCost(_pathfindingArgs, currentNode, openNeighborOne) != null)
            {
                return true;
            }

            if ((closedNeighborTwo == null || Utils.GetTileCost(_pathfindingArgs, currentNode, closedNeighborTwo) == null)
                && openNeighborTwo != null && Utils.GetTileCost(_pathfindingArgs, currentNode, openNeighborTwo) != null)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check to see if the node is a jump point (only works for cardinal directions)
        /// </summary>
        private bool IsCardinalJumpPoint(Direction direction, PathfindingNode currentNode)
        {
            PathfindingNode openNeighborOne;
            PathfindingNode closedNeighborOne;
            PathfindingNode openNeighborTwo;
            PathfindingNode closedNeighborTwo;

            switch (direction)
            {
                case Direction.North:
                    openNeighborOne = currentNode.GetNeighbor(Direction.NorthEast);
                    closedNeighborOne = currentNode.GetNeighbor(Direction.East);

                    openNeighborTwo = currentNode.GetNeighbor(Direction.NorthWest);
                    closedNeighborTwo = currentNode.GetNeighbor(Direction.West);
                    break;
                case Direction.East:
                    openNeighborOne = currentNode.GetNeighbor(Direction.NorthEast);
                    closedNeighborOne = currentNode.GetNeighbor(Direction.North);

                    openNeighborTwo = currentNode.GetNeighbor(Direction.SouthEast);
                    closedNeighborTwo = currentNode.GetNeighbor(Direction.South);
                    break;
                case Direction.South:
                    openNeighborOne = currentNode.GetNeighbor(Direction.SouthEast);
                    closedNeighborOne = currentNode.GetNeighbor(Direction.East);

                    openNeighborTwo = currentNode.GetNeighbor(Direction.SouthWest);
                    closedNeighborTwo = currentNode.GetNeighbor(Direction.West);
                    break;
                case Direction.West:
                    openNeighborOne = currentNode.GetNeighbor(Direction.NorthWest);
                    closedNeighborOne = currentNode.GetNeighbor(Direction.North);

                    openNeighborTwo = currentNode.GetNeighbor(Direction.SouthWest);
                    closedNeighborTwo = currentNode.GetNeighbor(Direction.South);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if ((closedNeighborOne == null || !Utils.Traversable(_pathfindingArgs.CollisionMask, closedNeighborOne.CollisionMask)) &&
                (openNeighborOne != null && Utils.Traversable(_pathfindingArgs.CollisionMask, openNeighborOne.CollisionMask)))
            {
                return true;
            }

            if ((closedNeighborTwo == null || !Utils.Traversable(_pathfindingArgs.CollisionMask, closedNeighborTwo.CollisionMask)) &&
                (openNeighborTwo != null && Utils.Traversable(_pathfindingArgs.CollisionMask, openNeighborTwo.CollisionMask)))
            {
                return true;
            }

            return false;
        }
    }
}
