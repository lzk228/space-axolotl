using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Components.Targets;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Objectives;

namespace Content.Server.Objectives.Systems;

public sealed class StealConditionSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!; // REMOVE ONE OF THESE!!!!!!!
    private EntityQuery<ContainerManagerComponent> _containerQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;

    public override void Initialize()
    {
        base.Initialize();

        _containerQuery = GetEntityQuery<ContainerManagerComponent>();
        _metaQuery = GetEntityQuery<MetaDataComponent>();

        SubscribeLocalEvent<StealConditionComponent, ObjectiveAssignedEvent>(OnAssigned);
        SubscribeLocalEvent<StealConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<StealConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    /// start checks of target acceptability, and generation of start values.
    private void OnAssigned(Entity<StealConditionComponent> condition, ref ObjectiveAssignedEvent args)
    {
        List<StealTargetComponent?> targetList = new();

        var query = AllEntityQuery<StealTargetComponent>();
        while (query.MoveNext(out var target))
        {
            if (condition.Comp.StealGroup != target.StealGroup)
                continue;

            targetList.Add(target);
        }

        // cancel if the required items do not exist
        if (targetList.Count == 0 && condition.Comp.VerifyMapExistence)
        {
            args.Cancelled = true;
            return;
        }

        //setup condition settings
        var maxSize = condition.Comp.VerifyMapExistence
            ? Math.Min(targetList.Count, condition.Comp.MaxCollectionSize)
            : condition.Comp.MaxCollectionSize;
        var minSize = condition.Comp.VerifyMapExistence
            ? Math.Min(targetList.Count, condition.Comp.MinCollectionSize)
            : condition.Comp.MinCollectionSize;

        condition.Comp.CollectionSize = _random.Next(minSize, maxSize);
    }

    //Set the visual, name, icon for the objective.
    private void OnAfterAssign(Entity<StealConditionComponent> condition, ref ObjectiveAfterAssignEvent args)
    {
        if (condition.Comp.ObjectiveText == null || condition.Comp.ObjectiveNoOwnerText == null
            || condition.Comp.DescriptionText == null || condition.Comp.DescriptionMultiplyText == null)
            return;

        var group = _proto.Index(condition.Comp.StealGroup);

        var title =condition.Comp.OwnerText == null
            ? Loc.GetString(condition.Comp.ObjectiveNoOwnerText, ("itemName", group.Name))
            : Loc.GetString(condition.Comp.ObjectiveText, ("owner", Loc.GetString(condition.Comp.OwnerText)), ("itemName", group.Name));

        var description = condition.Comp.CollectionSize > 1
            ? Loc.GetString(condition.Comp.DescriptionMultiplyText, ("itemName", group.Name), ("count", condition.Comp.CollectionSize))
            : Loc.GetString(condition.Comp.DescriptionText, ("itemName", group.Name));

        _metaData.SetEntityName(condition.Owner, title, args.Meta);
        _metaData.SetEntityDescription(condition.Owner, description, args.Meta);
        _objectives.SetIcon(condition.Owner, group.Sprite, args.Objective);
    }
    private void OnGetProgress(Entity<StealConditionComponent> condition, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = GetProgress(args.Mind, condition);
    }

    private float GetProgress(MindComponent mind, StealConditionComponent condition)
    {
        if (!_containerQuery.TryGetComponent(mind.OwnedEntity, out var currentManager))
            return 0;

        var stack = new Stack<ContainerManagerComponent>();
        var count = 0;

        //check pulling object
        if (TryComp<PullerComponent>(mind.OwnedEntity, out var pull)) //TO DO: to make the code prettier? don't like the repetition
        {
            var pulledEntity = pull.Pulling;
            if (pulledEntity != null)
            {
                // check if this is the item
                count += CheckStealTarget(pulledEntity.Value, condition);

                //we don't check the inventories of sentient entity
                if (!HasComp<MindContainerComponent>(pulledEntity))
                {
                    // if it is a container check its contents
                    if (_containerQuery.TryGetComponent(pulledEntity, out var containerManager))
                        stack.Push(containerManager);
                }
            }
        }

        // recursively check each container for the item
        // checks inventory, bag, implants, etc.
        do
        {
            foreach (var container in currentManager.Containers.Values)
            {
                foreach (var entity in container.ContainedEntities)
                {
                    // check if this is the item
                    count += CheckStealTarget(entity, condition);

                    // if it is a container check its contents
                    if (_containerQuery.TryGetComponent(entity, out var containerManager))
                        stack.Push(containerManager);
                }
            }
        } while (stack.TryPop(out currentManager));

        var result = count / (float) condition.CollectionSize;
        result = Math.Clamp(result, 0, 1);
        return result;
    }

    private int CheckStealTarget(EntityUid entity, StealConditionComponent condition)
    {
        // check if this is the target
        if (!TryComp<StealTargetComponent>(entity, out var target))
            return 0;

        if (target.StealGroup != condition.StealGroup)
            return 0;

        // check if needed target alive
        if (condition.CheckAlive)
        {
            if (TryComp<MobStateComponent>(entity, out var state))
            {
                if (!_mobState.IsAlive(entity, state))
                    return 0;
            }
        }
        return TryComp<StackComponent>(entity, out var stack) ? stack.Count : 1;
    }

    public void UpdateStealCondition(Entity<StealConditionComponent> entity, string stealGroup)
    {
        if (!_prototypeManager.TryIndex<StealTargetGroupPrototype>(stealGroup, out var stealGroupPrototype))
        {
            Log.Error($"Unknown steal prototype: {stealGroupPrototype}");
            return;
        }

        entity.Comp.StealGroup = stealGroup;
    }

    public void UpdateStealConditionNotify(Entity<StealConditionComponent> entity, string stealGroup, EntityUid mind)
    {
        UpdateStealCondition(entity, stealGroup);

        if (!TryComp<MindComponent>(mind, out var mindComp))
            return;

        var session = mindComp.Session;
        if (session == null)
            return;

        var msg = Loc.GetString("objective-condition-trade-updated-notification-message");
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", msg));
        _chatManager.ChatMessageToOne(ChatChannel.Server, msg, wrappedMessage, default, false, session.Channel, colorOverride: Color.Red);
    }
}
