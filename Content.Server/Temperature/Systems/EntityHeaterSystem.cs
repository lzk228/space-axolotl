﻿using Content.Server.Power.Components;
using Content.Shared.Placeable;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;

namespace Content.Server.Temperature.Systems;

public sealed class EntityHeaterSystem : SharedEntityHeaterSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntityHeaterComponent, PowerChangedEvent>(OnPowerChanged);
    }

    public override void Update(float deltaTime)
    {
        var query = EntityQueryEnumerator<EntityHeaterComponent, ItemPlacerComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var uid, out var comp, out var placer, out var power))
        {
            if (!power.Powered)
                continue;

            // don't divide by total entities since its a big grill
            // excess would just be wasted in the air but that's not worth simulating
            // if you want a heater thermomachine just use that...
            var energy = power.PowerReceived * deltaTime;
            foreach (var ent in placer.PlacedEntities)
            {
                Temperature.ChangeHeat(ent, energy);
            }
        }
    }

    private void OnPowerChanged(EntityUid uid, EntityHeaterComponent comp, ref PowerChangedEvent args)
    {
        // disable heating element glowing layer if theres no power
        // doesn't actually turn it off since that would be annoying
        var setting = args.Powered ? comp.Setting : EntityHeaterSetting.Off;
        Appearance.SetData(uid, EntityHeaterVisuals.Setting, setting);
    }

    public override void ChangeSetting(Entity<EntityHeaterComponent?> heater, EntityHeaterSetting setting)
    {
        ChangeSetting((heater, heater.Comp, null), setting);
    }

    public void ChangeSetting(Entity<EntityHeaterComponent?, ApcPowerReceiverComponent?> heater,
        EntityHeaterSetting setting)
    {
        base.ChangeSetting(heater, setting);
        if (!Resolve(heater,ref heater.Comp2))
            return;
        heater.Comp2.Load = SettingPower(setting, heater.Comp1!.Power);
        Appearance.SetData(heater, EntityHeaterVisuals.Setting, setting);
        Audio.PlayPvs(heater.Comp1.SettingSound, heater);
    }
}
