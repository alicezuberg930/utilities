namespace Utilities.Domain.Constants;

public static class FileConversionFormats
{
    public static readonly IReadOnlySet<string> AudioInputs = new HashSet<string>(StringComparer.Ordinal)
    {
        "aac",
        "flac",
        "m4a",
        "mp3",
        "ogg",
        "opus",
        "wav",
        "wma",
    };

    public static readonly IReadOnlySet<string> VideoInputs = new HashSet<string>(StringComparer.Ordinal)
    {
        "avi",
        "flv",
        "m4v",
        "mkv",
        "mov",
        "mp4",
        "mpeg",
        "mpg",
        "webm",
        "wmv",
    };

    public static readonly IReadOnlySet<string> AudioOutputs = new HashSet<string>(StringComparer.Ordinal)
    {
        "aac",
        "flac",
        "m4a",
        "mp3",
        "ogg",
        "opus",
        "wav",
    };

    public static readonly IReadOnlySet<string> VideoOutputs = new HashSet<string>(StringComparer.Ordinal)
    {
        "avi",
        "mkv",
        "mov",
        "mp4",
        "webm",
    };

    public static bool IsSupportedConversion(string originalFormat, string convertedFormat) =>
        originalFormat != convertedFormat &&
        (AudioInputs.Contains(originalFormat)
            ? AudioOutputs.Contains(convertedFormat)
            : VideoInputs.Contains(originalFormat) &&
              (AudioOutputs.Contains(convertedFormat) || VideoOutputs.Contains(convertedFormat)));
}
