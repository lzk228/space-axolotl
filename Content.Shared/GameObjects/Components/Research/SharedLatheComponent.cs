using System;
using System.Collections.Generic;
using Content.Shared.Materials;
using Content.Shared.Research;
using Mono.Cecil;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Content.Shared.GameObjects.Components.Research
{
    public enum LatheType
    {
        Autolathe,
        Protolathe,
    }

    public class SharedLatheComponent : Component
    {
        public override string Name => "Lathe";
        public override uint? NetID => ContentNetIDs.LATHE;
        [ViewVariables]
        public LatheType LatheType => _latheType;
        private LatheType _latheType = LatheType.Autolathe;

        public bool CanProduce(LatheRecipePrototype recipe, int quantity = 1)
        {
            Owner.TryGetComponent(out SharedMaterialStorageComponent storage);

            if (storage == null) return false;

            foreach (var (material, amount) in recipe.RequiredMaterials)
            {
                if (storage[material] <= (amount * quantity)) return false;
            }

            return true;
        }

        public bool CanProduce(string ID, int quantity = 1)
        {
            Owner.TryGetComponent(out SharedMaterialStorageComponent storage);

            if (storage == null) return false;

            var protMan = IoCManager.Resolve<IPrototypeManager>();
            protMan.TryIndex(ID, out LatheRecipePrototype recipe);

            if (recipe == null) return false;

            foreach (var (material, amount) in recipe.RequiredMaterials)
            {
                if (storage[material] <= (amount * quantity)) return false;
            }

            return true;
        }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref _latheType, "lathetype", LatheType.Autolathe);
        }

        /// <summary>
        ///     Sent to the server to sync material storage and the recipe queue.
        /// </summary>
        [Serializable, NetSerializable]
        public class LatheSyncRequestMessage : BoundUserInterfaceMessage
        {
            public LatheSyncRequestMessage()
            {
            }
        }

        /// <summary>
        ///     Sent to the client when the lathe is producing a recipe.
        /// </summary>
        [Serializable, NetSerializable]
        public class LatheProducingRecipeMessage : BoundUserInterfaceMessage
        {
            public readonly string ID;
            public LatheProducingRecipeMessage(string id)
            {
                ID = id;
            }
        }

        /// <summary>
        ///     Sent to the client when the lathe stopped/finished producing a recipe.
        /// </summary>
        [Serializable, NetSerializable]
        public class LatheStoppedProducingRecipeMessage : BoundUserInterfaceMessage
        {
            public LatheStoppedProducingRecipeMessage()
            {
            }
        }

        /// <summary>
        ///     Sent to the client to let it know about the recipe queue.
        /// </summary>
        [Serializable, NetSerializable]
        public class LatheFullQueueMessage : BoundUserInterfaceMessage
        {
            public readonly Queue<string> Recipes;
            public LatheFullQueueMessage(Queue<string> recipes)
            {
                Recipes = recipes;
            }
        }

        /// <summary>
        ///     Sent to the server when a client queues a new recipe.
        /// </summary>
        [Serializable, NetSerializable]
        public class LatheQueueRecipeMessage : BoundUserInterfaceMessage
        {
            public readonly string ID;
            public readonly int Quantity;
            public LatheQueueRecipeMessage(string id, int quantity)
            {
                ID = id;
                Quantity = quantity;
            }
        }

        [NetSerializable, Serializable]
        public enum LatheUiKey
        {
            Key,
        }
    }
}
