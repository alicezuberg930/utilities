namespace Utilities.Infrastructure.Configuration;

public sealed class FileConversionOptions
{
    public const string SectionName = "FileConversion";

    public string FfmpegExecutablePath { get; set; } = OperatingSystem.IsWindows()
        ? "ffmpeg.exe"
        : "/usr/bin/ffmpeg";

    public string FfprobeExecutablePath { get; set; } = OperatingSystem.IsWindows()
        ? "ffprobe.exe"
        : "/usr/bin/ffprobe";

    public string JobsDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "convert-jobs");
}
