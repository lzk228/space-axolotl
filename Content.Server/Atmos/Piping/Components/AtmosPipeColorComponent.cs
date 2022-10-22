using Robust.Shared.IoC;
using Content.Server.Atmos.Piping.EntitySystems;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Piping.Components
{
    [RegisterComponent]
    public sealed class AtmosPipeColorComponent : Component
    {
        [DataField("color")]
        public Color Color { get; set; } = Color.White;

        [ViewVariables(VVAccess.ReadWrite), UsedImplicitly]
        public Color ColorVV
        {
            get => Color;
            set => IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<AtmosPipeColorSystem>().SetColor(Owner, this, value);
        }
    }
}
