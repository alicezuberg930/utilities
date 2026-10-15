namespace Utilities.Domain.Constants;

public static class YoutubeFormats
{
    public const string Audio = "audio";
    public const string Video = "video";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Audio,
        Video,
    };
}

public static class JobStatuses
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public static class YoutubeQualities
{
    public static readonly IReadOnlySet<string> Video = new HashSet<string>(StringComparer.Ordinal)
    {
        "360p",
        "480p",
        "720p",
        "1080p",
    };
}
