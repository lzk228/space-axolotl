namespace Content.Shared.DisplacementMap;

[DataDefinition]
public sealed partial class DisplacementData
{
    /// <summary>
    /// allows you to attach different maps for layers of different sizes.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<int, PrototypeLayerData> DataBySize = default!;

    [DataField]
    public string? ShaderOverride = "DisplacedStencilDraw";
}
