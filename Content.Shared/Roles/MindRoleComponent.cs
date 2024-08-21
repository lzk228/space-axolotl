using Content.Shared.Mind;
using JetBrains.Annotations;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Roles;

/// <summary>
/// This holds data for, and indicates, a Mind Role entity
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MindRoleComponent : BaseMindRoleComponent
{
    /// <summary>
    ///     Marks this Mind Role as Antagonist
    ///     A single antag Mind Role is enough to make the owner mind count as Antagonist.
    /// </summary>
    [DataField]
    public bool Antag { get; set; } = false;

    /// <summary>
    ///     True if this mindrole is an exclusive antagonist. Antag setting is not checked if this is True.
    /// </summary>
    [DataField]
    public bool ExclusiveAntag { get; set; } = false; //TODO:ERRANT this is actually defined on the other prototypes, get it from there?

    /// <summary>
    ///     The time this role was created
    /// </summary>
    public TimeSpan Created { get; set; } = TimeSpan.Zero;

    /// <summary>
    ///     The Mind that this role belongs to
    /// </summary>
    public Entity<MindComponent> Mind { get; set; }




    // TODO:ERRANT Merge The prototypes into one?
    /// <summary>
    ///     The Antagonist prototype of this role
    /// </summary>
    [DataField]
    public ProtoId<AntagPrototype>? AntagPrototype { get; set; }

    /// <summary>
    ///     The Job prototype of this role
    /// </summary>
    [DataField]
    public ProtoId<JobPrototype>? JobPrototype { get; set; }

    [DataField] // Testing datafield for merged prototypes
    public ProtoId<IPrototype>? Proto { get; set; }

    //TODO: add Briefing?

    //TODO: Enum for Mind Role Type? (.Antag, .Job, .Misc) ?
}

public abstract partial class BaseMindRoleComponent : Component
{

}

/// <summary>
/// Mark the antagonist role component as being exclusive
/// IE by default other antagonists should refuse to select the same entity for a different antag role
/// </summary>
// TODO:ERRANT figure this out later
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[BaseTypeRequired(typeof(BaseMindRoleComponent))]
public sealed partial class ExclusiveAntagonistAttribute : Attribute
{

}
