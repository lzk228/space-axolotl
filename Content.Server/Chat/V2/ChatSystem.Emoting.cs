﻿using Content.Shared.Chat.Prototypes;
using Content.Shared.Chat.V2;
using Content.Shared.Chat.V2.Components;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Chat.V2;

public sealed partial class ChatSystem
{
    public void InitializeServerEmoting()
    {
        SubscribeNetworkEvent<AttemptEmoteEvent>((msg, args) => { HandleAttemptEmoteMessage(args.SenderSession, msg.Emoter, msg.Message); });
    }

    private void HandleAttemptEmoteMessage(ICommonSession player, NetEntity entity, string message)
    {
        var entityUid = GetEntity(entity);

        if (player.AttachedEntity != entityUid)
        {
            return;
        }

        if (IsRateLimited(entityUid, out var reason))
        {
            RaiseNetworkEvent(new EmoteFailedEvent(entity, reason), player);

            return;
        }

        if (!TryComp<EmoteableComponent>(entityUid, out var emoteable))
        {
            RaiseNetworkEvent(new EmoteFailedEvent(entity, Loc.GetString("chat-system-emote-failed")), player);

            return;
        }

        if (message.Length > MaxChatMessageLength)
        {
            RaiseNetworkEvent(new EmoteFailedEvent(entity, Loc.GetString("chat-system-max-message-length")), player);

            return;
        }

        SendEmoteMessage(entityUid, message, emoteable.Range);
    }

    /// <summary>
    /// Try and send an emote. If the emote contains some specific emote strings, they will also be emoted, to a max of 2 at a time.
    /// </summary>
    /// <param name="entityUid">The emoting entity. It needs an EmoteableComponent.</param>
    /// <param name="message">The emote message to send.</param>
    /// <param name="asName">The name to send.</param>
    /// <remarks>For example, "dances in circles lol" produces "dances in circles" and "laughs"</remarks>
    /// <returns></returns>
    public bool TrySendEmoteMessage(EntityUid entityUid, string message, string asName = "")
    {
        if (!TryComp<EmoteableComponent>(entityUid, out var emote))
            return false;

        SendEmoteMessage(entityUid, message, emote.Range, asName, false);

        return true;
    }

    /// <summary>
    /// Try and send an emote without causing any other messages to be sent afterward. Used to prevent recursion.
    /// </summary>
    /// <remarks>For example, Urist McShitter shouldn't be able to send "dances in circles lol lol lol lol" and emit five emotes.</remarks>
    /// <param name="entityUid"></param>
    /// <param name="message"></param>
    /// <param name="asName"></param>
    /// <returns></returns>
    public bool TrySendEmoteMessageWithoutRecursion(EntityUid entityUid, string message, string asName = "")
    {
        if (!TryComp<EmoteableComponent>(entityUid, out var emote))
            return false;

        SendEmoteMessage(entityUid, message, emote.Range, asName, true);

        return true;
    }

    /// <summary>
    /// Emote.
    /// </summary>
    /// <param name="entityUid">The entity who is emoting</param>
    /// <param name="message">The message to send.</param>
    /// <param name="range">The range the emote can be seen at</param>
    /// <param name="asName">Override the name this entity will appear as.</param>
    /// <param name="isRecursive">If this emote is being sent because of another message. Prevents multiple emotes being sent for the same input.</param>
    public void SendEmoteMessage(EntityUid entityUid, string message, float range, string asName = "", bool isRecursive = false)
    {
        message = SanitizeEmoteMessage(entityUid, message, out var emoteStr);

        if (!string.IsNullOrEmpty(emoteStr) && !isRecursive)
        {
            SendEmoteMessage(entityUid, emoteStr, range, asName, true);
        }

        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        // Mitigation for exceptions such as https://github.com/space-wizards/space-station-14/issues/24671
        try
        {
            message = FormattedMessage.RemoveMarkup(message);
        }
        catch (Exception e)
        {
            _logger.GetSawmill("chat").Error($"UID {entityUid} attempted to send {message} {(asName.Length > 0 ? "as name, " : "")} but threw a parsing error: {e}");

            return;
        }

        message = FormattedMessage.RemoveMarkup(message);

        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        if (string.IsNullOrEmpty(asName))
        {
            asName = Name(entityUid);
        }

        // Specifically don't capitalize if not already, because there's some arcane BS in the frontend that adds
        // "the" to the front of some emotes.
        var name = SanitizeName(asName, false);

        // This is a temporary workaround for weirdness with emotes where we ship around the prototype as a "success".
        var emote = GetEmote(message);
        if (emote != null)
        {
            var ev = new EmoteCreatedEvent(emote);
            RaiseLocalEvent(entityUid, ref ev);
        }

        var msgOut = new EmoteEvent(GetNetEntity(entityUid), name, message);

        RaiseLocalEvent(entityUid, msgOut, true);

        foreach (var session in GetEmoteRecipients(entityUid, range))
        {
            RaiseNetworkEvent(msgOut, session);
        }

        _replay.RecordServerMessage(msgOut);
        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Emote from {ToPrettyString(entityUid):user} as {asName}: {message}");
    }

    private List<ICommonSession> GetEmoteRecipients(EntityUid source, float range)
    {
        var recipients = new List<ICommonSession>();

        var ghostHearing = GetEntityQuery<GhostHearingComponent>();
        var xforms = GetEntityQuery<TransformComponent>();

        var transformSource = xforms.GetComponent(source);
        var sourceMapId = transformSource.MapID;
        var sourceCoords = transformSource.Coordinates;

        foreach (var player in _playerManager.Sessions)
        {
            if (player.AttachedEntity is not { Valid: true } playerEntity)
                continue;

            var transformEntity = xforms.GetComponent(playerEntity);

            if (transformEntity.MapID != sourceMapId)
                continue;

            // even if they are a ghost hearer, in some situations we still need the range
            if (ghostHearing.HasComponent(playerEntity) || sourceCoords.TryDistance(EntityManager, transformEntity.Coordinates, out var distance) && distance < range)
                recipients.Add(player);
        }

        return recipients;
    }

    public void TryEmoteWithChat(EntityUid source, string emoteId, string nameOverride = "")
    {
        if (!TryComp<EmoteableComponent>(source, out var comp))
            return;

        if (!_proto.TryIndex<EmotePrototype>(emoteId, out var emote))
            return;

        // check if proto has valid message for chat
        if (emote.ChatMessages.Count != 0)
        {
            SendEmoteMessage(source, Loc.GetString(_random.Pick(emote.ChatMessages), ("entity", source)), comp.Range, nameOverride);
        }
        else
        {
            // do the rest of emote event logic here
            TryEmoteWithoutChat(source, emoteId);
        }
    }

    public void TryEmoteWithoutChat(EntityUid source, string emoteId)
    {
        if (!TryComp<EmoteableComponent>(source, out var comp))
            return;

        if (!_proto.TryIndex<EmotePrototype>(emoteId, out var emote))
            return;

        var ev = new EmoteCreatedEvent(emote);
        RaiseLocalEvent(source, ref ev);
    }

    private string SanitizeEmoteMessage(EntityUid source, string message, out string? emoteStr)
    {
        return SanitizeMessage(source, message, false, out emoteStr);
    }
}
