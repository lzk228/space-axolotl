using Content.Shared.Examine;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared.Construction.Steps
{
    [DataDefinition]
    public sealed partial class ToolConstructionGraphStep : ConstructionGraphStep
    {
        [DataField(required:true)]
        public ProtoId<ToolQualityPrototype> Tool { get; private set; }

        [DataField("fuel")] public float Fuel { get; private set; } = 10;

        [DataField("examine")] public string ExamineOverride { get; private set; } = string.Empty;

        public override void DoExamine(ExaminedEvent examinedEvent)
        {
            if (!string.IsNullOrEmpty(ExamineOverride))
            {
                examinedEvent.PushMarkup(Loc.GetString(ExamineOverride));
                return;
            }

            if (string.IsNullOrEmpty(Tool) || !IoCManager.Resolve<IPrototypeManager>().TryIndex(Tool, out ToolQualityPrototype? quality))
                return;

            examinedEvent.PushMarkup(Loc.GetString("construction-use-tool-entity", ("toolName", Loc.GetString(quality.ToolName))));

        }

        public override ConstructionGuideEntry GenerateGuideEntry()
        {
            var quality = IoCManager.Resolve<IPrototypeManager>().Index<ToolQualityPrototype>(Tool);

            return new ConstructionGuideEntry()
            {
                Localization = "construction-presenter-tool-step",
                Arguments = new (string, object)[]{("tool", quality.ToolName)},
                Icon = quality.Icon,
            };
        }
    }
}
