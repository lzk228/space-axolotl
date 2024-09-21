using Content.Server.Chat.Systems;
using Content.Server.Electrocution;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Server.PowerCell;
using Content.Server.Traits.Assorted;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Damage;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Medical;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server.Medical;

/// <summary>
/// This handles interactions and logic relating to <see cref="DefibrillatorComponent"/>
/// </summary>
public sealed class DefibrillatorSystem : SharedDefibrillatorSystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly ChatSystem _chatManager = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly SharedRottingSystem _rotting = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    public override void Zap(EntityUid uid, EntityUid target, EntityUid user, DefibrillatorComponent component, MobStateComponent? mob = null)
    {
        base.Zap(uid, target, user, component, mob);

        _electrocution.TryDoElectrocution(target, null, component.ZapDamage, component.WritheDuration, true, ignoreInsulation: true);

        // TODO : powercell should be rewritten to shared instead of strictly be on Server side
        if (!_powerCell.TryUseActivatableCharge(uid, user: user))
            return;

        ICommonSession? session = null;

        var dead = true;
        if (_rotting.IsRotten(target))
        {
            _chatManager.TrySendInGameICMessage(uid, Loc.GetString("defibrillator-rotten"),
                InGameICChatType.Speak, true);
        }
        else if (HasComp<UnrevivableComponent>(target))
        {
            _chatManager.TrySendInGameICMessage(uid, Loc.GetString("defibrillator-unrevivable"),
                InGameICChatType.Speak, true);
        }
        else
        {
            if (_mobState.IsDead(target, mob))
                _damageable.TryChangeDamage(target, component.ZapHeal, true, origin: uid);

            if (_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out var threshold) &&
                TryComp<DamageableComponent>(target, out var damageableComponent) &&
                damageableComponent.TotalDamage < threshold)
            {
                _mobState.ChangeMobState(target, MobState.Critical, mob, uid);
                dead = false;
            }

            if (_mind.TryGetMind(target, out _, out var mind) &&
                mind.Session is { } playerSession)
            {
                session = playerSession;
                // notify them they're being revived.
                if (mind.CurrentEntity != target)
                {
                    _euiManager.OpenEui(new ReturnToBodyEui(mind, _mind), session);
                }
            }
            else
            {
                _chatManager.TrySendInGameICMessage(uid, Loc.GetString("defibrillator-no-mind"),
                    InGameICChatType.Speak, true);
            }
        }

        var sound = dead || session == null
            ? component.FailureSound
            : component.SuccessSound;
        _audio.PlayPvs(sound, uid);

        // if we don't have enough power left for another shot, turn it off
        if (!_powerCell.HasActivatableCharge(uid))
            _toggle.TryDeactivate(uid);

        // TODO clean up this clown show above
        var ev = new TargetDefibrillatedEvent(user, (uid, component));
        RaiseLocalEvent(target, ref ev);

        Dirty(uid, component);
    }
}
