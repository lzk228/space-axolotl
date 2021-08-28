using Content.Server.Light.Components;
using Content.Server.Light.Events;
using Content.Shared.Light;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using System;

namespace Content.Server.Light.EntitySystems
{
    public class UnpoweredFlashlightSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<UnpoweredFlashlightComponent, TryToggleLightEvent>(OnToggleLight);
        }

        private void OnToggleLight(EntityUid uid, UnpoweredFlashlightComponent component, TryToggleLightEvent args)
        {
            ToggleLight(component);
        }

        public void ToggleLight(UnpoweredFlashlightComponent flashlight)
        {
            if (!flashlight.Owner.TryGetComponent(out PointLightComponent? light))
                return;

            flashlight.LightOn = !flashlight.LightOn;
            light.Enabled = flashlight.LightOn;

            if (flashlight.Owner.TryGetComponent(out AppearanceComponent? appearance))
                appearance.SetData(UnpoweredFlashlightVisuals.LightOn, flashlight.LightOn);

            SoundSystem.Play(Filter.Pvs(light.Owner), flashlight.ToggleSound.GetSound(), flashlight.Owner);
        }

    }
}
