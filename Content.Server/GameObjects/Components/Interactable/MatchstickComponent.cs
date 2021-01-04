﻿#nullable enable
using System.Threading.Tasks;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Server.GameObjects;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components.Timers;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Interactable
{
    [RegisterComponent]
    [ComponentReference(typeof(IHotItem))]
    public class MatchstickComponent : Component, IHotItem, IInteractUsing, IAfterInteract
    {
        public override string Name => "Matchstick";

        private MatchstickState _currentState;

        /// <summary>
        /// How long will matchstick last in seconds.
        /// </summary>
        [ViewVariables(VVAccess.ReadOnly)] private int _duration;

        /// <summary>
        /// Sound played when you ignite the matchstick.
        /// </summary>
        private string? _igniteSound;

        /// <summary>
        /// Point light component.
        /// </summary>
        private PointLightComponent? _pointLightComponent;

        /// <summary>
        /// Current state to matchstick. Can be <code>Unlit</code>, <code>Lit</code> or <code>Burnt</code>.
        /// </summary>
        [ViewVariables]
        public MatchstickState CurrentState
        {
            get => _currentState;
            private set
            {
                _currentState = value;

                if (_pointLightComponent != null)
                {
                    if (_currentState == MatchstickState.Lit)
                    {
                        _pointLightComponent.Enabled = true;
                    }
                    else
                    {
                        _pointLightComponent.Enabled = false;
                    }
                }

                if (Owner.TryGetComponent(out AppearanceComponent? appearance))
                {
                    appearance.SetData(MatchstickVisual.Igniting, _currentState);
                }
            }
        }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref _duration, "duration", 10);
            serializer.DataField(ref _igniteSound, "igniteSound", null);
        }

        public override void Initialize()
        {
            base.Initialize();

            CurrentState = MatchstickState.Unlit;

            Owner.TryGetComponent(out _pointLightComponent);
        }

        public bool IsCurrentlyHot()
        {
            return CurrentState == MatchstickState.Lit;
        }

        public void Ignite(IEntity user)
        {
            // Play Sound
            if (!string.IsNullOrEmpty(_igniteSound))
            {
                EntitySystem.Get<AudioSystem>().PlayFromEntity(_igniteSound, user,
                    AudioHelpers.WithVariation(0.125f).WithVolume(-0.125f));
            }

            // Change state
            CurrentState = MatchstickState.Lit;
            Owner.SpawnTimer(_duration * 1000, () => CurrentState = MatchstickState.Burnt);
        }

        public async Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            if (eventArgs.Target.TryGetComponent<IHotItem>(out var hotItem)
                && hotItem.IsCurrentlyHot()
                && CurrentState == MatchstickState.Unlit)
            {
                Ignite(eventArgs.User);
                return true;
            }

            return false;
        }

        public async Task AfterInteract(AfterInteractEventArgs eventArgs)
        {
            if (eventArgs.Target.TryGetComponent<MatchboxComponent>(out _))
            {
                Ignite(eventArgs.User);
            }
        }
    }
}