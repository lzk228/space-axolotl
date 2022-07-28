using System.Diagnostics.CodeAnalysis;
using Content.Server.AI.Components;

namespace Content.Server.AI.Systems;

public sealed partial class NPCSystem
{
    private readonly Dictionary<string, object> _blackboardDefaults = new()
    {
        {"VisionRadius", 7f}
    };

    /// <summary>
    /// Tries to get the blackboard data for a particular key. Returns default if not found
    /// </summary>
    public T? GetValueOrDefault<T>(NPCComponent component, string key)
    {
        if (component.BlackboardA.TryGetValue(key, out var value))
        {
            return (T) value;
        }

        if (_blackboardDefaults.TryGetValue(key, out value))
        {
            return (T) value;
        }

        return default;
    }

    /// <summary>
    /// Tries to get the blackboard data for a particular key.
    /// </summary>
    public bool TryGetValue<T>(NPCComponent component, string key, [NotNullWhen(true)] out T? value)
    {
        if (component.BlackboardA.TryGetValue(key, out var data))
        {
            value = (T) data;
            return true;
        }

        value = default;
        return false;
    }

    /*
    * Constants to make development easier
    */

    public const string VisionRadius = "VisionRadius";
}
