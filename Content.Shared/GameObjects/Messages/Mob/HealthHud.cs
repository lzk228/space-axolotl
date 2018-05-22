﻿using SS14.Shared.GameObjects;
using SS14.Shared.Serialization;
using System;

namespace Content.Shared.GameObjects
{
    /// <summary>
    /// Sends updates to the standard species health hud with the sprite to change the hud to
    /// </summary>
    [Serializable, NetSerializable]
    public class HudStateChange : ComponentMessage
    {
        public string StateSprite;
    }
}
