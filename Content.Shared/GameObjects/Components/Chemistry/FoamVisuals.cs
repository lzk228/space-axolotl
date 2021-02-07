﻿#nullable enable
using System;
using Robust.Shared.Serialization;

namespace Content.Shared.GameObjects.Components.Chemistry
{
    [Serializable, NetSerializable]
    public enum FoamVisuals : byte
    {
        State,
        Color
    }
}
