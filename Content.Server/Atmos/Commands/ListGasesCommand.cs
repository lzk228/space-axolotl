﻿using Content.Server.Administration;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Atmos.Commands
{
    [AdminCommand(AdminFlags.Debug)]
    public sealed class ListGasesCommand : IConsoleCommand
    {
        [Dependency] private readonly IEntitySystemManager _esMan = default!;

        public string Command => "listgases";
        public string Description => "Prints a list of gases and their indices.";
        public string Help => "listgases";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var atmosSystem = _esMan.GetEntitySystem<AtmosphereSystem>();

            foreach (var gasPrototype in atmosSystem.Gases)
            {
                shell.WriteLine($"{gasPrototype.Name} ID: {gasPrototype.ID}");
            }
        }
    }

}
