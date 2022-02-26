using System.Diagnostics.CodeAnalysis;
using System.IO;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared.Administration;

/// <summary>
///     Manager that allows resources to be added at runtime by admins.
///     They will be sent to all clients automatically.
/// </summary>
public abstract class SharedNetworkResourceManager : IContentRoot
{
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] protected readonly IResourceManager ResourceManager = default!;

    public const double BytesToMegabytes = 0.000001d;

    /// <summary>
    ///     The prefix for any and all downloaded network resources.
    /// </summary>
    private static readonly ResourcePath Prefix = ResourcePath.Root / "Uploaded";

    protected readonly Dictionary<ResourcePath, byte[]> Files = new();

    public virtual void Initialize()
    {
        _netManager.RegisterNetMessage<NetworkResourceUploadMessage>(ResourceUploadMsg);

        // Add ourselves as a content root.
        ResourceManager.AddRoot(Prefix, this);
    }

    protected abstract void ResourceUploadMsg(NetworkResourceUploadMessage msg);

    public bool TryGetFile(ResourcePath relPath, [NotNullWhen(true)] out Stream? stream)
    {
        byte[]? data;

        lock(Files)
        {
            if (!Files.TryGetValue(relPath, out data))
            {
                stream = null;
                return false;
            }
        }

        // Non-writable stream, as this needs to be thread-safe.
        stream = new MemoryStream(data, false);
        return true;
    }

    public IEnumerable<ResourcePath> FindFiles(ResourcePath path)
    {
        lock(Files)
        {
            foreach (var (file, _) in Files)
            {
                if (file.TryRelativeTo(path, out _))
                    yield return file;
            }
        }
    }

    public IEnumerable<string> GetRelativeFilePaths()
    {
        lock (Files)
        {
            foreach (var (file, _) in Files)
            {
                yield return file.ToString();
            }
        }
    }

    public void Mount()
    {
        // Nada. We don't need to perform any special logic here.
    }
}
