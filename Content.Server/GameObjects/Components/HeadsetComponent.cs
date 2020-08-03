﻿using Robust.Shared.GameObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Server.GameObjects.Components
{
    [RegisterComponent]
    public class HeadsetComponent : Component
    {
        public override string Name => "Headset";

        public override void Initialize()
        {
            base.Initialize();

        }

        public void Test()
        {
            Console.WriteLine("Test functional.");
        }
    }
}
