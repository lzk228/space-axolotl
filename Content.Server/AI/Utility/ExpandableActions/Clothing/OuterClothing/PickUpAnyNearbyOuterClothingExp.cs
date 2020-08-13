using System;
using System.Collections.Generic;
using Content.Server.AI.Utility.Actions;
using Content.Server.AI.Utility.Actions.Clothing.OuterClothing;
using Content.Server.AI.Utility.Considerations;
using Content.Server.AI.Utility.Considerations.Clothing;
using Content.Server.AI.Utility.Considerations.Inventory;
using Content.Server.AI.WorldState;
using Content.Server.AI.WorldState.States;
using Content.Server.AI.WorldState.States.Clothing;
using Content.Server.GameObjects.Components.Items.Clothing;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.IoC;

namespace Content.Server.AI.Utility.ExpandableActions.Clothing.OuterClothing
{
    public sealed class PickUpAnyNearbyOuterClothingExp : ExpandableUtilityAction
    {
        public override float Bonus => UtilityAction.NormalBonus;

        protected override IEnumerable<Func<float>> GetCommonConsiderations(Blackboard context)
        {
            var considerationsManager = IoCManager.Resolve<ConsiderationsManager>();

            return new[]
            {
                considerationsManager.Get<ClothingInSlotCon>().Slot(EquipmentSlotDefines.Slots.OUTERCLOTHING, context)
                    .InverseBoolCurve(context),
                considerationsManager.Get<ClothingInInventoryCon>().Slot(EquipmentSlotDefines.SlotFlags.OUTERCLOTHING, context)
                    .InverseBoolCurve(context)
            };
        }

        public override IEnumerable<UtilityAction> GetActions(Blackboard context)
        {
            var owner = context.GetState<SelfState>().GetValue();

            foreach (var entity in context.GetState<NearbyClothingState>().GetValue())
            {
                if (entity.TryGetComponent(out ClothingComponent clothing) &&
                    (clothing.SlotFlags & EquipmentSlotDefines.SlotFlags.OUTERCLOTHING) != 0)
                {
                    yield return new PickUpOuterClothing(owner, entity, Bonus);
                }
            }
        }
    }
}
