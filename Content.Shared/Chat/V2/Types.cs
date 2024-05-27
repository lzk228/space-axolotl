using Content.Shared.Chat.V2.Systems;

namespace Content.Shared.Chat.V2;

/// <summary>
/// The types of messages that can be sent, validated and processed via user input that are covered by Chat V2.
/// </summary>
public enum MessageType : byte
{
    #region Player-sendable types

    /// <summary>
    /// Chat for announcements like CentCom telling you to stop sending them memes.
    /// </summary>
    Announcement,
    /// <summary>
    /// Chat that ghosts use to complain about being gibbed.
    /// </summary>
    DeadChat,
    /// <summary>
    /// Chat that mimes use to evade their vow.
    /// </summary>
    Emote,
    /// <summary>
    /// Chat that players use to make lame jokes to people nearby.
    /// </summary>
    Local,
    /// <summary>
    /// Chat that players use to complain about shitsec/admins/antags/balance/etc.
    /// </summary>
    Looc,
    /// <summary>
    /// Chat that players use to say "HELP MAINT", or plead to call the shuttle because a beaker spilled.
    /// </summary>
    /// <remarks>This does not tell you what radio channel has been chatted on!</remarks>
    Radio,
    /// <summary>
    /// Chat that is used exclusively by syndie tots to collaborate on whatever tots do.
    /// </summary>
    Whisper,

    #endregion

    #region Non-player-sendable types

    /// <summary>
    /// Chat that is sent to exactly one player; almost exclusively used for admemes and prayer responses.
    /// </summary>
    Subtle,
    /// <summary>
    /// Chat that is sent by automata, like when a vending machine thanks you for your unwise purchases.
    /// </summary>
    Background,

    #endregion
}

/// <summary>
/// Defines a very generic chat event.
/// </summary>
public interface IChatEvent
{
    public ChatContext Context
    {
        get;
    }

    /// <summary>
    /// The sender of the chat message.
    /// </summary>
    public EntityUid Sender
    {
        get;
    }

    /// <summary>
    /// The ID of the message. This is overwritten when saved into a repository.
    /// </summary>
    public uint Id
    {
        get;
        set;
    }

    /// <summary>
    /// The sent message.
    /// </summary>
    public string Message
    {
        get;
        set;
    }

    /// <summary>
    /// The type of sent message.
    /// </summary>
    public MessageType Type
    {
        get;
    }

    public void SetId(uint id)
    {
        if (Id != 0)
        {
            return;
        }

        Id = id;
    }
}

/// <summary>
/// Covers the volumes a verbal chat message can be sent with.
/// </summary>
public enum VerbalVolume : byte
{
    // Thought, not spoken, and thus totally silent. Used for telepathic-like communications using other systems like radio.
    Internal,
    // Short-range, obfuscated if too far away, no voice ID if too far away and no visuals
    Whisper,
    // Normal range.
    Talk,
    // Normal range but BIGGER TEXT!!
    Shout,
    // The entity breaks the fourth wall. Works just like Talk but the message is flagged as OOC.
    OutOfCharacter
}

public enum OutOfCharacterChatChannel : byte
{
    Dead,
    OutOfCharacter,
    Admin,
    System
}
