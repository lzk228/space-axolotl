﻿using System;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared.Inventory;

[Prototype("inventoryTemplate")]
public sealed class InventoryTemplatePrototype : IPrototype
{
    [DataField("id", required: true)]
    public string ID { get; } = string.Empty;

    [DataField("slots")]
    public SlotDefinition[] Slots { get; } = Array.Empty<SlotDefinition>();
}

[DataDefinition]
public sealed class SlotDefinition
{
    [DataField("name", required: true)] public string Name { get; } = string.Empty;
    [DataField("slotTexture")] public string TextureName { get; } = "pocket";
    [DataField("slotFlags")] public SlotFlags SlotFlags { get; } = SlotFlags.PREVENTEQUIP;
    [DataField("showInWindow")] public bool ShowInWindow { get; } =true;
    [DataField("slotGroup")] public string SlotGroup { get; } ="";

    [DataField("uiWindowPos", required: true)] public Vector2i UIWindowPosition { get; }
    [DataField("dependsOn")] public string? DependsOn { get; }
    [DataField("displayName", required: true)] public string DisplayName { get; } = string.Empty;

    /// <summary>
    ///     Offset for the clothing sprites.
    /// </summary>
    [DataField("offset")] public Vector2 Offset { get; } = Vector2.Zero;
}
