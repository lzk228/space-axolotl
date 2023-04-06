using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.RCD.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.RCD.Systems;

public sealed class RCDAmmoSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RCDAmmoComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<RCDAmmoComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnExamine(EntityUid uid, RCDAmmoComponent comp, ExaminedEvent args)
    {
        var examineMessage = Loc.GetString("rcd-ammo-component-on-examine-text", ("charges", comp.Charges));
        args.PushText(examineMessage);
    }

    private void OnAfterInteract(EntityUid uid, RCDAmmoComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !_timing.IsFirstTimePredicted)
            return;

        if (args.Target is not {Valid: true} target ||
            !TryComp<RCDComponent>(target, out var rcd))
            return;

        var user = args.User;
        args.Handled = true;
        var count = Math.Min(rcd.MaxCharges - rcd.Charges, comp.Charges);
        if (count <= 0)
        {
            if (_net.IsClient)
                _popup.PopupEntity(Loc.GetString("rcd-ammo-component-after-interact-full-text"), target, user);
            return;
        }

        if (_net.IsClient)
            _popup.PopupEntity(Loc.GetString("rcd-ammo-component-after-interact-refilled-text"), target, user);
        rcd.Charges += count;
        comp.Charges -= count;

        // prevent having useless ammo with 0 charges
        if (comp.Charges <= 0)
            QueueDel(uid);
    }
}
