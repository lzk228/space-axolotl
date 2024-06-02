﻿using Content.Server.Administration.Managers;
using Content.Shared.Chat.V2.Components;
using Content.Shared.Chat.V2.Systems;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Chat.V2.Systems;

public sealed class AttachedEntityValidationSystem : EntitySystem
{
    private const string EntityNotOwnedBySender = "chat-system-entity-not-owned-by-sender";
    private string _entityNotOwnedBySender = "";

    public override void Initialize()
    {
        base.Initialize();

        _entityNotOwnedBySender = Loc.GetString(EntityNotOwnedBySender);

        SubscribeLocalEvent<ChatValidationEvent<AttemptVerbalChatEvent>>(OnValidationEvent);
        SubscribeLocalEvent<ChatValidationEvent<AttemptVisualChatEvent>>(OnValidationEvent);
        SubscribeLocalEvent<ChatValidationEvent<AttemptAnnouncementEvent>>(OnValidationEvent);
    }

    private void OnValidationEvent<T>(ChatValidationEvent<T> msg, EntitySessionEventArgs args) where T : ChatAttemptEvent
    {
        if (args.SenderSession.AttachedEntity != null || args.SenderSession.AttachedEntity != GetEntity(msg.Event.Sender))
            msg.Cancel(_entityNotOwnedBySender);
    }
}
