using Content.Server.Power.Components;
using Content.Shared.Examine;
using JetBrains.Annotations;

namespace Content.Server.Power.EntitySystems
{
    [UsedImplicitly]
    public sealed class BatterySystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ExaminableBatteryComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<ExaminableAnchoredPowerComponent, ExaminedEvent>(OnAnchoredExamine);

            SubscribeLocalEvent<NetworkBatteryPreSync>(PreSync);
            SubscribeLocalEvent<NetworkBatteryPostSync>(PostSync);
        }

        private void OnExamine(EntityUid uid, ExaminableBatteryComponent component, ExaminedEvent args)
        {
            if (!TryComp<BatteryComponent>(uid, out var batteryComponent))
                return;
            if (args.IsInDetailsRange)
            {
                var effectiveMax = batteryComponent.MaxCharge;
                if (effectiveMax == 0)
                    effectiveMax = 1;
                var chargeFraction = batteryComponent.CurrentCharge / effectiveMax;
                var chargePercentRounded = (int) (chargeFraction * 100);
                args.PushMarkup(
                    Loc.GetString(
                        "examinable-battery-component-examine-detail",
                        ("percent", chargePercentRounded),
                        ("markupPercentColor", "green")
                    )
                );
            }
        }

        private void OnAnchoredExamine(EntityUid uid, ExaminableAnchoredPowerComponent component, ExaminedEvent args)
        {
            if (!Comp<TransformComponent>(uid).Anchored)
                args.PushMarkup(
                    Loc.GetString(
                        "examinable-anchored-power-unconnected",
                        ("target", uid)
                    )
                );
        }

        private void PreSync(NetworkBatteryPreSync ev)
        {
            foreach (var (netBat, bat) in EntityManager.EntityQuery<PowerNetworkBatteryComponent, BatteryComponent>())
            {
                netBat.NetworkBattery.Capacity = bat.MaxCharge;
                netBat.NetworkBattery.CurrentStorage = bat.CurrentCharge;
            }
        }

        private void PostSync(NetworkBatteryPostSync ev)
        {
            foreach (var (netBat, bat) in EntityManager.EntityQuery<PowerNetworkBatteryComponent, BatteryComponent>())
            {
                bat.CurrentCharge = netBat.NetworkBattery.CurrentStorage;
            }
        }

        public override void Update(float frameTime)
        {
            foreach (var (comp, batt) in EntityManager.EntityQuery<BatterySelfRechargerComponent, BatteryComponent>())
            {
                if (!comp.AutoRecharge) continue;
                if (batt.IsFullyCharged) continue;
                batt.CurrentCharge += comp.AutoRechargeRate * frameTime;
            }
        }
    }
}
