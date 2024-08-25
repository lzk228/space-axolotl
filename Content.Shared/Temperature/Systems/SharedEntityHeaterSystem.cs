using Content.Shared.Temperature.Components;
using Content.Shared.Examine;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.Temperature.Systems;

/// <summary>
/// Handles <see cref="EntityHeaterComponent"/> updating and events.
/// </summary>
public abstract class SharedEntityHeaterSystem : EntitySystem
{
    [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] protected readonly SharedTemperatureSystem Temperature = default!;
    [Dependency] protected readonly SharedAudioSystem Audio = default!;

    private readonly int SettingCount = Enum.GetValues(typeof(EntityHeaterSetting)).Length;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntityHeaterComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<EntityHeaterComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnExamined(EntityUid uid, EntityHeaterComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("entity-heater-examined", ("setting", comp.Setting)));
    }

    private void OnGetVerbs(EntityUid uid, EntityHeaterComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var setting = (int) comp.Setting;
        setting++;
        setting %= SettingCount;
        var nextSetting = (EntityHeaterSetting) setting;

        args.Verbs.Add(new AlternativeVerb()
        {
            Text = Loc.GetString("entity-heater-switch-setting", ("setting", nextSetting)),
            Act = () =>
            {
                ChangeSetting((uid, comp), nextSetting);
                Popup.PopupEntity(Loc.GetString("entity-heater-switched-setting", ("setting", nextSetting)), uid, args.User);
            }
        });
    }

    public virtual void ChangeSetting(Entity<EntityHeaterComponent?> heater, EntityHeaterSetting setting)
    {
        if (!Resolve(heater, ref heater.Comp))
            return;
        heater.Comp.Setting = setting;
        Dirty(heater);
    }

    protected float SettingPower(EntityHeaterSetting setting, float max)
    {
        switch (setting)
        {
            case EntityHeaterSetting.Low:
                return max / 3f;
            case EntityHeaterSetting.Medium:
                return max * 2f / 3f;
            case EntityHeaterSetting.High:
                return max;
            default:
                return 0f;
        }
    }
}
