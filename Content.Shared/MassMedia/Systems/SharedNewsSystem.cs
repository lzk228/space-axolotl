using JetBrains.Annotations;
using Robust.Shared.Utility;

namespace Content.Shared.MassMedia.Systems;

[Serializable]
public struct NewsArticle
{
    public string Name;
    public string Content;
    public TimeSpan ShareTime;
}
