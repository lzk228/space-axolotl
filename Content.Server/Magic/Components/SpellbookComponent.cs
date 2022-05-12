﻿using System.Threading;
using Content.Shared.Actions.ActionTypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Server.Magic;

[RegisterComponent]
public sealed class SpellbookComponent : Component
{
    /// <summary>
    /// List of spells that this book has.
    /// </summary>
    [ViewVariables]
    public readonly List<ActionType> Spells = new();

    /// <summary>
    /// The three fields below is just used for initialisation.
    /// </summary>
    [DataField("worldSpells", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<int, WorldTargetActionPrototype>))]
    public readonly Dictionary<string, int> WorldSpells = new();

    [DataField("entitySpells", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<int, EntityTargetActionPrototype>))]
    public readonly Dictionary<string, int> EntitySpells = new();

    [DataField("instantSpells", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<int, InstantActionPrototype>))]
    public readonly Dictionary<string, int> InstantSpells = new();

    [ViewVariables]
    [DataField("learnTime")]
    public float LearnTime = .75f;

    public CancellationTokenSource? CancelToken;
}
