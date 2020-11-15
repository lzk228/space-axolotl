﻿#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Damage;
using Content.Shared.GameObjects.Components.Damage;
using Robust.Server.Interfaces.Console;
using Robust.Server.Interfaces.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.Commands
{
    [AdminCommand(AdminFlags.Fun)]
    class HurtCommand : IClientCommand
    {
        public string Command => "hurt";
        public string Description => "Ouch";
        public string Help => $"Usage: {Command} <type/?> <amount> (<entity uid/_>) (<ignoreResistances>)";

        private string DamageTypes()
        {
            var msg = new StringBuilder();
            foreach (var dClass in Enum.GetNames(typeof(DamageClass)))
            {
                msg.Append($"\n{dClass}");

                var types = Enum.Parse<DamageClass>(dClass).ToTypes();

                if (types.Count > 0)
                {
                    msg.Append(": ");
                    msg.AppendJoin('|', types);
                }
            }

            return $"Damage Types:{msg}";
        }

        private delegate void Damage(IDamageableComponent damageable, bool ignoreResistances);

        private bool TryParseEntity(IConsoleShell shell, IPlayerSession? player, string arg,
            [NotNullWhen(true)] out IEntity? entity)
        {
            entity = null;

            if (arg == "_")
            {
                var playerEntity = player?.AttachedEntity;

                if (playerEntity == null)
                {
                    shell.SendText(player, $"You must have a player entity to use this command without specifying an entity.\n{Help}");
                    return false;
                }

                entity = playerEntity;
                return true;
            }

            if (!EntityUid.TryParse(arg, out var entityUid))
            {
                shell.SendText(player, $"{arg} is not a valid entity uid.\n{Help}");

                return false;
            }

            var entityManager = IoCManager.Resolve<IEntityManager>();

            if (!entityManager.TryGetEntity(entityUid, out var parsedEntity))
            {
                shell.SendText(player, $"No entity found with uid {entityUid}");

                return false;
            }

            entity = parsedEntity;
            return true;
        }

        private bool TryParseDamageArgs(
            IConsoleShell shell,
            IPlayerSession? player,
            string[] args,
            [NotNullWhen(true)] out Damage? func)
        {
            if (!int.TryParse(args[1], out var amount))
            {
                shell.SendText(player, $"{args[1]} is not a valid damage integer.");

                func = null;
                return false;
            }

            if (Enum.TryParse<DamageClass>(args[0], true, out var damageClass))
            {
                func = (damageable, ignoreResistances) =>
                {
                    if (!damageable.DamageClasses.ContainsKey(damageClass))
                    {
                        shell.SendText(player, $"Entity {damageable.Owner.Name} with id {damageable.Owner.Uid} can not be damaged with damage class {damageClass}");

                        return;
                    }

                    if (!damageable.ChangeDamage(damageClass, amount, ignoreResistances))
                    {
                        shell.SendText(player, $"Entity {damageable.Owner.Name} with id {damageable.Owner.Uid} received no damage.");

                        return;
                    }

                    var response =
                        $"Damaged entity {damageable.Owner.Name} with id {damageable.Owner.Uid} for {amount} {damageClass} damage{(ignoreResistances ? ", ignoring resistances." : ".")}";

                    shell.SendText(player, response);
                };

                return true;
            }
            // Fall back to DamageType
            else if (Enum.TryParse<DamageType>(args[0], true, out var damageType))
            {
                func = (damageable, ignoreResistances) =>
                {
                    if (!damageable.DamageTypes.ContainsKey(damageType))
                    {
                        shell.SendText(player, $"Entity {damageable.Owner.Name} with id {damageable.Owner.Uid} can not be damaged with damage class {damageType}");

                        return;
                    }

                    if (!damageable.ChangeDamage(damageType, amount, ignoreResistances))
                    {
                        shell.SendText(player, $"Entity {damageable.Owner.Name} with id {damageable.Owner.Uid} received no damage.");

                        return;
                    }

                    var response =
                        $"Damaged entity {damageable.Owner.Name} with id {damageable.Owner.Uid} for {amount} {damageType} damage{(ignoreResistances ? ", ignoring resistances." : ".")}";

                    shell.SendText(player, response);
                };

                return true;
            }
            else
            {
                shell.SendText(player, $"{args[0]} is not a valid damage class or type.");

                var types = DamageTypes();
                shell.SendText(player, types);

                func = null;
                return false;
            }
        }

        public void Execute(IConsoleShell shell, IPlayerSession? player, string[] args)
        {
            bool ignoreResistances;
            IEntity entity;
            Damage? damageFunc;

            switch (args.Length)
            {
                // Check if we have enough for the dmg types to show
                case var n when n > 0 && (args[0] == "?" || args[0] == "¿"):
                    var types = DamageTypes();

                    if (args[0] == "¿")
                    {
                        types = types.Replace("e", "é");
                    }

                    shell.SendText(player, types);

                    return;
                // Not enough args
                case var n when n < 2:
                    shell.SendText(player, $"Invalid number of arguments.\n{Help}");
                    return;
                case var n when n >= 2 && n <= 4:
                    if (!TryParseDamageArgs(shell, player, args, out damageFunc))
                    {
                        return;
                    }

                    var entityUid = n == 2 ? "_" : args[2];

                    if (!TryParseEntity(shell, player, entityUid, out var parsedEntity))
                    {
                        return;
                    }

                    entity = parsedEntity;

                    if (n == 4)
                    {
                        if (!bool.TryParse(args[3], out ignoreResistances))
                        {
                            shell.SendText(player, $"{args[3]} is not a valid boolean value for ignoreResistances.\n{Help}");
                            return;
                        }
                    }
                    else
                    {
                        ignoreResistances = false;
                    }

                    break;
                default:
                    shell.SendText(player, $"Invalid amount of arguments.\n{Help}");
                    return;
            }

            if (!entity.TryGetComponent(out IDamageableComponent? damageable))
            {
                shell.SendText(player, $"Entity {entity.Name} with id {entity.Uid} does not have a {nameof(IDamageableComponent)}.");
                return;
            }

            damageFunc(damageable, ignoreResistances);
        }
    }
}
