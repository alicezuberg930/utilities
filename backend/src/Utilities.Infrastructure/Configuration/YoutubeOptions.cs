namespace Utilities.Infrastructure.Configuration;

public sealed class YoutubeOptions
{
    public const string SectionName = "Youtube";

    public string ExecutablePath { get; set; } = OperatingSystem.IsWindows()
        ? "yt-dlp.exe"
        : "/usr/bin/yt-dlp";

    public string JobsDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "jobs");
}
