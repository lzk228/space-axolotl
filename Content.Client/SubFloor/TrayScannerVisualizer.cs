using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client.SubFloor;

public class TrayScannerVisualizer : AppearanceVisualizer
{
    public override void OnChangeData(AppearanceComponent component)
    {
        base.OnChangeData(component);

        if (!component.Owner.TryGetComponent(out SpriteComponent? sprite))
            return;

        if (!component.TryGetData(TrayScannerTransparency.Key, out bool transparent))
            return;

        foreach (var layer in sprite.AllLayers)
        {
            var transparency = transparent == true ? 0.8f : 1f;
            layer.Color = layer.Color.WithAlpha(transparency);
        }

        if (sprite.LayerMapTryGet(SubFloorShowLayerVisualizer.Layers.FirstLayer, out var firstLayer))
        {
            sprite.LayerSetColor(firstLayer, Color.White);
        }
    }
}

public enum TrayScannerTransparency
{
    Key,
}
