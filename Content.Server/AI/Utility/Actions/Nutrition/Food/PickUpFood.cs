using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.Operators.Inventory;
using Content.Server.AI.Operators.Movement;
using Content.Server.AI.Utility.AiLogic;
using Content.Server.AI.Utility.Considerations;
using Content.Server.AI.Utility.Considerations.Hands;
using Content.Server.AI.Utility.Considerations.Movement;
using Content.Server.AI.Utility.Considerations.Nutrition;
using Content.Server.AI.Utility.Curves;
using Content.Server.AI.WorldState;
using Content.Server.AI.WorldState.States;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.Utility.Actions.Nutrition.Food
{
    public sealed class PickUpFood : UtilityAction
    {
        private IEntity _entity;

        public PickUpFood(IEntity owner, IEntity entity, BonusWeight weight) : base(owner)
        {
            _entity = entity;
            Bonus = weight;
        }

        public override void SetupOperators(Blackboard context)
        {
            ActionOperators = new Queue<IOperator>(new IOperator[]
            {
                new MoveToEntityOperator(Owner, _entity),
                new PickupEntityOperator(Owner, _entity),
            });
        }

        protected override Consideration[] Considerations => new Consideration[]
        {
            new TargetNotInAnyHandsCon(
                new BoolCurve()),
            new FreeHandCon(
                new BoolCurve()),
            new HungerCon(
                new LogisticCurve(1000f, 1.3f, -1.0f, 0.5f)),
            new DistanceCon(
                new QuadraticCurve(1.0f, 1.0f, 0.02f, 0.0f)),
            new FoodValueCon(
                new QuadraticCurve(1.0f, 0.4f, 0.0f, 0.0f)),
        };

        protected override void UpdateBlackboard(Blackboard context)
        {
            base.UpdateBlackboard(context);
            context.GetState<TargetEntityState>().SetValue(_entity);
        }
    }
}
