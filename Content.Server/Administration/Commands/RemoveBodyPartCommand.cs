using Content.Server.Body.Components;
using Content.Shared.Administration;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems.Body;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands
{
    [AdminCommand(AdminFlags.Fun)]
    public sealed class RemoveBodyPartCommand : IConsoleCommand
    {
        public string Command => "rmbodypart";
        public string Description => "Removes a given entity from it's containing body, if any.";
        public string Help => "Usage: rmbodypart <uid>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1)
            {
                shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
                return;
            }

            if (!EntityUid.TryParse(args[0], out var entityUid))
            {
                shell.WriteError(Loc.GetString("shell-entity-uid-must-be-number"));
                return;
            }

            var entityManager = IoCManager.Resolve<IEntityManager>();

            if (!entityManager.TryGetComponent<TransformComponent>(entityUid, out var transform)) return;

            var parent = transform.ParentUid;

            var bodySys = EntitySystem.Get<SharedBodySystem>();

            if (entityManager.TryGetComponent<BodyComponent>(parent, out var body) &&
                entityManager.TryGetComponent<SharedBodyPartComponent>(entityUid, out var part))
            {
                bodySys.RemovePart(parent, part, body);
            }
            else
            {
                shell.WriteError("Was not a body part, or did not have a parent.");
            }
        }
    }
}
