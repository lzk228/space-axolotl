using Robust.Shared.Prototypes;

namespace Content.Server.AI.Components
{
    [Prototype("aiFaction")]
    public sealed class AiFactionPrototype : IPrototype
    {
        // These are immutable so any dynamic changes aren't saved back over.
        // AiFactionSystem will just read these and then store them.
        [ViewVariables]
        [IdDataFieldAttribute]
        public string ID { get; } = default!;

        [DataField("hostile")]
        public IReadOnlyList<string> Hostile { get; private set; } = new List<string>();
    }
}
