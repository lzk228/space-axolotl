using Content.Server.NodeContainer;
using Content.Server.Power.Components;
using Content.Server.Power.NodeGroups;
using Content.Server.Tools;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Server.Power.EntitySystems
{
    [UsedImplicitly]
    public sealed class CableMultitoolSystem : EntitySystem
    {
        [Dependency] private readonly ToolSystem _toolSystem = default!;
        [Dependency] private readonly PowerNetSystem _pnSystem = default!;
        [Dependency] private readonly ExamineSystemShared _examineSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<CableComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
        }

        private void OnGetExamineVerbs(EntityUid uid, CableComponent component, GetVerbsEvent<ExamineVerb> args)
        {
            // Must be in details range to try this.
            // Theoretically there should be a separate range at which a multitool works, but this does just fine.
            if (_examineSystem.IsInDetailsRange(args.User, args.Target))
            {
                var held = args.Using;

                // Pulsing is hardcoded here because I don't think it needs to be more complex than that right now.
                // Update if I'm wrong.
                var enabled = held != null && _toolSystem.HasQuality(held.Value, "Pulsing");
                var verb = new ExamineVerb
                {
                    Disabled = !enabled,
                    Message = Loc.GetString("cable-multitool-system-verb-tooltip"),
                    Text = Loc.GetString("cable-multitool-system-verb-name"),
                    Category = VerbCategory.Examine,
                    IconTexture = "/Textures/Interface/VerbIcons/zap.svg.192dpi.png",
                    Act = () =>
                    {
                        var markup = FormattedMessage.FromMarkup(GenerateCableMarkup(uid));
                        _examineSystem.SendExamineTooltip(args.User, uid, markup, false, false);
                    }
                };

                args.Verbs.Add(verb);
            }
        }

        private string GenerateCableMarkup(EntityUid uid, NodeContainerComponent? nodeContainer = null)
        {
            if (!Resolve(uid, ref nodeContainer))
                return Loc.GetString("cable-multitool-system-internal-error-missing-component");

            foreach (var node in nodeContainer.Nodes)
            {
                if (!(node.Value.NodeGroup is IBasePowerNet))
                    continue;
                var p = (IBasePowerNet) node.Value.NodeGroup;
                var ps = _pnSystem.GetNetworkStatistics(p.NetworkNode);

                double storageRatio = ps.InStorageCurrent / Math.Max(ps.InStorageMax, 1.0);
                double outStorageRatio = ps.OutStorageCurrent / Math.Max(ps.OutStorageMax, 1.0);
                return Loc.GetString("cable-multitool-system-statistics",
                    ("supplyc", ps.SupplyCurrent),
                    ("supplyb", ps.SupplyBatteries),
                    ("supplym", ps.SupplyTheoretical),
                    ("consumption", ps.Consumption),
                    ("storagec", ps.InStorageCurrent),
                    ("storager", storageRatio),
                    ("storagem", ps.InStorageMax),
                    ("storageoc", ps.OutStorageCurrent),
                    ("storageor", outStorageRatio),
                    ("storageom", ps.OutStorageMax)
                );
            }
            return Loc.GetString("cable-multitool-system-internal-error-no-power-node");
        }
    }
}
