﻿using Content.Shared.Chat.TypingIndicator;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Chat.TypingIndicator;

public sealed class TypingIndicatorVisualizerSystem : VisualizerSystem<TypingIndicatorComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    protected override void OnAppearanceChange(EntityUid uid, TypingIndicatorComponent component, ref AppearanceChangeEvent args)
    {
        base.OnAppearanceChange(uid, component, ref args);

        if (!TryComp(uid, out SpriteComponent? sprite))
            return;
        
        if (!_prototypeManager.TryIndex<TypingIndicatorPrototype>(component.Prototype, out var proto))
        {
            Logger.Error($"Unknown typing indicator id: {component.Prototype}");
            return;
        }

        args.Component.TryGetData(TypingIndicatorVisuals.IsTyping, out bool isTyping);
        var isLayerExist = sprite.LayerMapTryGet(TypingIndicatorLayers.Base, out var layer);
        if (!isLayerExist)
            layer = sprite.LayerMapReserveBlank(TypingIndicatorLayers.Base);
        
        sprite.LayerSetRSI(layer, proto.SpritePath);
        sprite.LayerSetState(layer, proto.TypingState);
        sprite.LayerSetShader(layer, proto.Shader);
        sprite.LayerSetOffset(layer, proto.Offset);
        sprite.LayerSetVisible(layer, isTyping);
    }
}
