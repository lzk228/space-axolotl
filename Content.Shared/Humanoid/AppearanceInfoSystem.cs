using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Species.Components;


namespace Content.Shared.Humanoid;

public sealed partial class AppearanceInfoSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AppearanceInfoComponent, ComponentInit>(OnCompInit);
    }

    private void OnCompInit(EntityUid uid, AppearanceInfoComponent comp, ComponentInit args)
    {
        if (comp.Fetched == true || !TryComp<HumanoidAppearanceComponent>(uid, out var humanoidAppearance))
            return;

        comp.Appearance = humanoidAppearance;
        comp.Name = Identity.Name(uid, EntityManager);
        comp.Fetched = true;
    }
}
