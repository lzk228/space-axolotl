using Content.Server.Anomaly.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Anomaly.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server.Anomaly;

/// <summary>
/// a device that allows you to translate anomaly activity into multitool signals.
/// </summary>
public sealed partial class AnomalySynchronizerSystem : EntitySystem
{
    [Dependency] private readonly AnomalySystem _anomaly = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly DeviceLinkSystem _signal = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnomalySynchronizerComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<AnomalySynchronizerComponent, PowerChangedEvent>(OnPowerChanged);

        SubscribeLocalEvent<AnomalyPulseEvent>(OnAnomalyPulse);
        SubscribeLocalEvent<AnomalySupercriticalEvent>(OnAnomalySupercritical);
        SubscribeLocalEvent<AnomalyStabilityChangedEvent>(OnAnomalyStabilityChanged);
    }

    private void OnPowerChanged(EntityUid uid, AnomalySynchronizerComponent component, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        if (!TryComp<AnomalyComponent>(component.ConnectedAnomaly, out var anomaly))
            return;

        _anomaly.DoAnomalyPulse(component.ConnectedAnomaly.Value, anomaly);
        DisconnetFromAnomaly(uid, component, anomaly);
    }

    private void OnInteractHand(EntityUid uid, AnomalySynchronizerComponent component, InteractHandEvent args)
    {
        Log.Debug("Тык");
        foreach (var entity in _entityLookup.GetEntitiesInRange(uid, 0.15f)) //is the radius of one tile. It must not be set higher, otherwise the anomaly can be moved from tile to tile
        {
            if (TryComp<AnomalyComponent>(entity, out var anomaly) && _power.IsPowered(uid))
            {
                ConnectToAnomaly(uid, component, entity, anomaly);
                break;
            }
        }
    }

    private void ConnectToAnomaly(EntityUid uid, AnomalySynchronizerComponent component, EntityUid auid, AnomalyComponent anomaly)
    {
        Log.Debug("Начинается подключение");
        if (component.ConnectedAnomaly == auid)
            return;

        component.ConnectedAnomaly = auid;
        //move the anomaly to the center of the synchronizer, for aesthetics.
        var targetXform = _transform.GetWorldPosition(uid);
        _transform.SetWorldPosition(auid, targetXform);

        _anomaly.DoAnomalyPulse(component.ConnectedAnomaly.Value, anomaly);
        _popup.PopupEntity(Loc.GetString("anomaly-sync-connected"), uid, PopupType.Medium);
        _audio.PlayPvs(component.ConnectedSound, uid);

        Log.Debug("Присоединена аномалия: " + component.ConnectedAnomaly.Value);
    }

    //TO DO: disconnection from the anomaly should also be triggered if the anomaly is far away from the synchronizer.
    //Currently only bluespace anomaly can do this, but for some reason it is the only one that cannot be connected to the synchronizer.
    private void DisconnetFromAnomaly(EntityUid uid, AnomalySynchronizerComponent component, AnomalyComponent anomaly)
    {
        Log.Debug("Начинается отключение");
        if (component.ConnectedAnomaly == null)
            return;

        _anomaly.DoAnomalyPulse(component.ConnectedAnomaly.Value, anomaly);
        _popup.PopupEntity(Loc.GetString("anomaly-sync-disconnected"), uid, PopupType.Large);
        _audio.PlayPvs(component.ConnectedSound, uid);

        component.ConnectedAnomaly = default!;
        Log.Debug("Отключено");
    }

    private void OnAnomalyPulse(ref AnomalyPulseEvent args)
    {
        Log.Debug("Что-то пульсирует");
        var query = EntityQueryEnumerator<AnomalySynchronizerComponent>();
        while (query.MoveNext(out var ent, out var component))
        {
            if (args.Anomaly != component.ConnectedAnomaly)
                continue;

            var uid = args.Anomaly;
            if (_power.IsPowered(uid))
                continue;

            _signal.InvokePort(uid, component.PulsePort);
            Log.Debug("Я ПУЛЬСИРУЮ!");
        }
    }

    private void OnAnomalySupercritical(ref AnomalySupercriticalEvent args)
    {
        var query = EntityQueryEnumerator<AnomalySynchronizerComponent>();
        while (query.MoveNext(out var ent, out var component))
        {
            if (args.Anomaly != component.ConnectedAnomaly)
                continue;

            var uid = args.Anomaly;
            if (_power.IsPowered(uid))
                continue;

            _signal.InvokePort(uid, component.SupercritPort);
        }
    }
    private void OnAnomalyStabilityChanged(ref AnomalyStabilityChangedEvent args)
    {
        Log.Debug("Что-то изменилось");
        var query = EntityQueryEnumerator<AnomalySynchronizerComponent>();
        while (query.MoveNext(out var ent, out var component))
        {
            if (args.Anomaly != component.ConnectedAnomaly)
                continue;

            var uid = args.Anomaly;
            if (_power.IsPowered(uid))
                continue;

            if (args.Stability < 0.25f) //I couldn't find where these values are stored, so I hardcoded them. Tell me where these variables are stored and I'll fix it
            {
                _signal.InvokePort(uid, component.DecayingPort);
                Log.Debug("РАЗЛАГАЮСЬ");
            }
            else if (args.Stability > 0.5f) //I couldn't find where these values are stored, so I hardcoded them. Tell me where these variables are stored and I'll fix it
            {
                _signal.InvokePort(uid, component.GrowingPort);
                Log.Debug("Норм");
            }
            else
            {
                _signal.InvokePort(uid, component.StabilizePort);
                Log.Debug("РАСТУ");
            }
        }
    }
}
