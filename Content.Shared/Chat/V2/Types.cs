using Content.Shared.Chat.V2.Systems;

namespace Content.Shared.Chat.V2;

/// <summary>
/// Defines a generic chat event.
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
    public NetEntity Sender
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

    public void SetId(uint id)
    {
        if (Id != 0)
        {
            return;
        }

        Id = id;
    }
}
