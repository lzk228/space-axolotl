﻿#nullable enable
using Content.Server.GameObjects.EntitySystems;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components.Transform;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Atmos
{
    [RegisterComponent]
    public class AirtightComponent : Component, IMapInit
    {
        private (GridId, MapIndices) _lastPosition;

        public override string Name => "Airtight";

        private bool _airBlocked = true;
        private bool _fixVacuum = false;

        [ViewVariables(VVAccess.ReadWrite)]
        public bool AirBlocked
        {
            get => _airBlocked;
            set
            {
                _airBlocked = value;

                if (SnapGrid != null)
                {
                    EntitySystem.Get<AtmosphereSystem>().GetGridAtmosphere(Owner.Transform.GridID)?.Invalidate(SnapGrid.Position);
                }
            }
        }

        [ViewVariables]
        public bool FixVacuum => _fixVacuum;

        private SnapGridComponent? SnapGrid => Owner.TryGetComponent(out SnapGridComponent snapGrid) ? snapGrid : null;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref _airBlocked, "airBlocked", true);
            serializer.DataField(ref _fixVacuum, "fixVacuum", true);
        }

        public override void Initialize()
        {
            base.Initialize();

            // Using the SnapGrid is critical for the performance of the room builder, and thus if
            // it is absent the component will not be airtight. A warning is much easier to track
            // down than the object magically not being airtight, so log one if the SnapGrid component
            // is missing.
            if (!Owner.EnsureComponent(out SnapGridComponent _))
                Logger.Warning($"Entity {Owner} at {Owner.Transform.MapPosition.ToString()} doesn't have a {nameof(SnapGridComponent)}");

            UpdatePosition();
        }

        public void MapInit()
        {
            if (SnapGrid != null)
            {
                SnapGrid.OnPositionChanged += OnTransformMove;
                _lastPosition = (Owner.Transform.GridID, SnapGrid.Position);
            }

            UpdatePosition();
        }

        protected override void Shutdown()
        {
            base.Shutdown();

            _airBlocked = false;

            if (SnapGrid != null)
            {
                SnapGrid.OnPositionChanged -= OnTransformMove;

                if (_fixVacuum)
                    EntitySystem.Get<AtmosphereSystem>().GetGridAtmosphere(Owner.Transform.GridID)?
                        .FixVacuum(SnapGrid.Position);
            }


            UpdatePosition();
        }

        private void OnTransformMove()
        {
            UpdatePosition(_lastPosition.Item1, _lastPosition.Item2);
            UpdatePosition();

            if (SnapGrid != null)
            {
                _lastPosition = (Owner.Transform.GridID, SnapGrid.Position);
            }
        }

        private void UpdatePosition()
        {
            if (SnapGrid != null)
            {
                UpdatePosition(Owner.Transform.GridID, SnapGrid.Position);
            }
        }

        private void UpdatePosition(GridId gridId, MapIndices pos)
        {
            EntitySystem.Get<AtmosphereSystem>().GetGridAtmosphere(gridId)?.Invalidate(pos);
        }
    }
}
