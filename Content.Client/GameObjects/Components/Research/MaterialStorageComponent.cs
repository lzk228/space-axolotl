using System;
using System.Collections.Generic;
using Content.Shared.GameObjects.Components.Research;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.ViewVariables;

namespace Content.Client.GameObjects.Components.Research
{
    public class MaterialStorageComponent : SharedMaterialStorageComponent
    {
        protected override Dictionary<string, int> Storage => _storage;
        private Dictionary<string, int> _storage = new Dictionary<string, int>();

        public event Action OnMaterialStorageChanged;

        public override void HandleMessage(ComponentMessage message, INetChannel netChannel = null, IComponent component = null)
        {
            base.HandleMessage(message, netChannel, component);

            switch (message)
            {
                case MaterialStorageUpdateMessage msg:
                    _storage = msg.Storage;
                    OnMaterialStorageChanged?.Invoke();
                    break;

            }
        }
    }
}
