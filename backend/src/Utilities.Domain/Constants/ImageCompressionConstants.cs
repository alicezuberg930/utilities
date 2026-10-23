namespace Utilities.Domain.Constants;

public static class ImageCompressionFormats
{
    public static readonly IReadOnlySet<string> SupportedInputs =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "jpeg",
            "jpg",
            "png",
            "webp",
        };

    public static bool IsSupportedInput(string format) => SupportedInputs.Contains(format);

    public static string GetOutputFormat(string originalFormat, bool convertToWebp)
    {
        if (convertToWebp && originalFormat is not "webp")
        {
            return "webp";
        }

        return originalFormat switch
        {
            "jpeg" or "jpg" => "jpg",
            "png" => "png",
            "webp" => "webp",
            _ => throw new ArgumentOutOfRangeException(
                nameof(originalFormat),
                originalFormat,
                "The image format is not supported for compression."),
        };
    }
}
