using Content.Server.Speech;
using Content.Shared.Chemistry.Reagent;

namespace Content.Server.Chemistry.ReagentEffects;

/// <summary>
///     Forces someone to scream their lungs out.
/// </summary>
public sealed class Scream : ReagentEffect
{
    public override void Effect(ReagentEffectArgs args)
    {
        IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<VocalSystem>().TryScream(args.SolutionEntity);
    }
}
