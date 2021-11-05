using Content.Server.Body;
using Content.Server.Body.Part;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Server.Administration.Commands
{
    [AdminCommand(AdminFlags.Fun)]
    public class RemoveBodyPartCommand : IConsoleCommand
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

            if (!entityManager.TryGetComponent<ITransformComponent>(entityUid, out var transform)) return;

            var parent = transform.ParentUid;

            if (entityManager.TryGetComponent<BodyComponent>(parent, out var body) &&
                entityManager.TryGetComponent<BodyPartComponent>(entityUid, out var part))
            {
                body.RemovePart(part);

            }
            else
            {
                shell.WriteError("Was not a body part, or did not have a parent.");
            }
        }
    }
}
