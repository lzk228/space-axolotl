using Content.Server.EUI;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Server.Administration.UI;

/// <summary>
///     Admin Eui for spawning and preview-ing explosions
/// </summary>
[UsedImplicitly]
public sealed class SpawnExplosionEui : BaseEui
{
    [Dependency] private readonly IEntitySystemManager _esMan = default!;

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is SpawnExplosionEuiMsg.Close)
        {
            Close();
            return;
        }

        if (msg is not SpawnExplosionEuiMsg.PreviewRequest request)
            return;

        if (request.TotalIntensity <= 0 || request.IntensitySlope <= 0)
            return;

        var explosion = _esMan.GetEntitySystem<ExplosionSystem>().GenerateExplosionPreview(request);

        if (explosion == null)
        {
            Logger.Error("Failed to generate explosion preview.");
            return;
        }

        SendMessage(new SpawnExplosionEuiMsg.PreviewData(explosion, request.IntensitySlope, request.TotalIntensity));
    }
}
