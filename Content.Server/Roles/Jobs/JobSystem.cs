﻿using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Content.Server.Chat.Managers;
using Content.Server.Mind;
using Content.Server.Players;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Roles.Jobs;

public sealed class JobSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<JobComponent, RoleAddedEvent>(MindOnDoGreeting);
    }

    private void MindOnDoGreeting(EntityUid mindId, JobComponent component, RoleAddedEvent args)
    {
        if (!_mind.TryGetSession(mindId, out var session))
            return;

        if (!MindTryGetJob(mindId, out _, out var prototype))
            return;

        _chat.DispatchServerMessage(session, Loc.GetString("job-greet-introduce-job-name",
            ("jobName", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(prototype.LocalizedName))));

        if (prototype.RequireAdminNotify)
            _chat.DispatchServerMessage(session, Loc.GetString("job-greet-important-disconnect-admin-notify"));

        _chat.DispatchServerMessage(session, Loc.GetString("job-greet-supervisors-warning",
            ("jobName", Name),
            ("supervisors", Loc.GetString(prototype.Supervisors))));
    }

    public void MindAddJob(EntityUid mind, string jobPrototypeId)
    {
        if (MindHasJobWithId(mind, jobPrototypeId))
            return;

        _mind.AddRole(mind, new JobComponent { PrototypeId = jobPrototypeId });
    }

    public bool MindHasJobWithId(EntityUid? mindId, string prototypeId)
    {
        return CompOrNull<JobComponent>(mindId)?.PrototypeId == prototypeId;
    }

    public bool MindTryGetJob(
        [NotNullWhen(true)] EntityUid? mindId,
        [NotNullWhen(true)] out JobComponent? comp,
        [NotNullWhen(true)] out JobPrototype? prototype)
    {
        comp = null;
        prototype = null;

        return TryComp(mindId, out comp) &&
               comp.PrototypeId != null &&
               _prototypes.TryIndex(comp.PrototypeId, out prototype);
    }

    /// <summary>
    ///     Tries to get the job name for this mind.
    ///     Returns unknown if not found.
    /// </summary>
    public bool MindTryGetJobName([NotNullWhen(true)] EntityUid? mindId, out string name)
    {
        if (MindTryGetJob(mindId, out _, out var prototype))
        {
            name = prototype.LocalizedName;
            return true;
        }

        name = Loc.GetString("generic-unknown-title");
        return false;
    }

    /// <summary>
    ///     Tries to get the job name for this mind.
    ///     Returns unknown if not found.
    /// </summary>
    public string MindTryGetJobName([NotNullWhen(true)] EntityUid? mindId)
    {
        MindTryGetJobName(mindId, out var name);
        return name;
    }

    public string MindGetStartingJob(EntityUid mindId)
    {
        if (!MindTryGetJob(mindId, out _, out var prototype))
            return string.Empty;

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(prototype.Name);
    }

    public bool CanBeAntag(IPlayerSession player)
    {
        if (player.ContentData() is not { Mind: { } mindId })
            return false;

        if (!MindTryGetJob(mindId, out _, out var prototype))
            return true;

        return prototype.CanBeAntag;
    }
}
