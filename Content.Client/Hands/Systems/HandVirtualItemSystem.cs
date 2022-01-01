using Content.Client.Items;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using JetBrains.Annotations;

namespace Content.Client.Hands.Systems
{
    [UsedImplicitly]
    public sealed class HandVirtualItemSystem : SharedHandVirtualItemSystem
    {
        public override void Initialize()
        {
            base.Initialize();

            Subs.ItemStatus<HandVirtualItemComponent>(_ => new HandVirtualItemStatus());
        }
    }
}
