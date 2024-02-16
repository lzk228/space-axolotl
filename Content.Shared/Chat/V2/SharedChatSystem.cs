﻿using System.Globalization;
using Content.Shared.Administration.Managers;
using Content.Shared.CCVar;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Chat.V2;

public abstract partial class SharedChatSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminManager _admin = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] protected readonly IConfigurationManager Configuration = default!;

    protected bool ShouldCapitalizeTheWordI;
    protected bool ShouldPunctuate;
    protected int MaxChatMessageLength;

    public override void Initialize()
    {
        base.Initialize();

        ShouldCapitalizeTheWordI = (!CultureInfo.CurrentCulture.IsNeutralCulture && CultureInfo.CurrentCulture.Parent.Name == "en")
                                    || (CultureInfo.CurrentCulture.IsNeutralCulture && CultureInfo.CurrentCulture.Name == "en");
        ShouldPunctuate = Configuration.GetCVar(CCVars.ChatPunctuation);
        MaxChatMessageLength = Configuration.GetCVar(CCVars.ChatMaxMessageLength);

        Configuration.OnValueChanged(CCVars.ChatPunctuation, shouldPunctuate => ShouldPunctuate = shouldPunctuate);
        Configuration.OnValueChanged(CCVars.ChatMaxMessageLength, maxLen => MaxChatMessageLength = maxLen);

        InitializeEmote();
    }
}
