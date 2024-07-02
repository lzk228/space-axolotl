using Content.Shared.Mobs.Components;

namespace Content.Shared.Mobs.Systems
{
    /// <summary>
    ///     Adds ItemComponent to entity when it dies. Remove when it revives.
    /// </summary>
    public sealed class AddCompOnMobStateChangeSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AddCompOnMobStateChangeComponent, MobStateChangedEvent>(OnMobStateChanged);
        }

        private void OnMobStateChanged(EntityUid uid, AddCompOnMobStateChangeComponent component, MobStateChangedEvent args)
        {
            if(!TryComp<MobStateComponent>(uid, out var mobState))
            return;

            if (mobState.CurrentState == component.MobState)
            {
                foreach (var compType in component.Components)
                {
                    EntityManager.AddComponents(uid, component.Components);
                }
            }
            else
            {
                foreach (var compType in component.Components)
                {
                    EntityManager.RemoveComponents(uid, component.Components);
                }
            }
        }
    }
}
