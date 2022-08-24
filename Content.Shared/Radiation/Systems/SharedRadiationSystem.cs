using Content.Shared.Radiation.Components;
using Robust.Shared.Map;
using FloodFillSystem = Content.Shared.FloodFill.FloodFillSystem;

namespace Content.Shared.Radiation.Systems;

public abstract partial class SharedRadiationSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly FloodFillSystem _floodFill = default!;

    private const float RadiationCooldown = 1.0f;
    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        InitRadBlocking();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;

        while (_accumulator > RadiationCooldown)
        {
            _accumulator -= RadiationCooldown;

            UpdateRadSources();
            UpdateReceivers();
        }
    }


}
