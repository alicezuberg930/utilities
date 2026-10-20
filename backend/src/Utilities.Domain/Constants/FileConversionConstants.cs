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

    public static readonly IReadOnlySet<string> ImageInputs = new HashSet<string>(StringComparer.Ordinal)
    {
        "apng",
        "avif",
        "bmp",
        "cri",
        "dds",
        "dng",
        "dpx",
        "exr",
        "fits",
        "gif",
        "hdr",
        "ico",
        "im1",
        "im8",
        "im24",
        "im32",
        "img",
        "j2c",
        "j2k",
        "jls",
        "jp2",
        "jpc",
        "jpeg",
        "jpg",
        "jps",
        "jxl",
        "ljpg",
        "mng",
        "mpo",
        "msp",
        "pam",
        "pbm",
        "pcd",
        "pcx",
        "pct",
        "pfm",
        "pgm",
        "pgmyuv",
        "pgx",
        "phm",
        "pic",
        "pict",
        "pix",
        "png",
        "pnm",
        "pns",
        "ppm",
        "psd",
        "ptx",
        "qoi",
        "ras",
        "rs",
        "rw2",
        "sgi",
        "sun",
        "sunras",
        "svg",
        "svgz",
        "tga",
        "tif",
        "tiff",
        "timg",
        "vbn",
        "wbmp",
        "webp",
        "xbm",
        "xface",
        "ximg",
        "xpm",
        "xwd",
    };

    public static readonly IReadOnlySet<string> ImageOutputs = new HashSet<string>(StringComparer.Ordinal)
    {
        "apng",
        "avif",
        "bmp",
        "dpx",
        "exr",
        "fits",
        "gif",
        "hdr",
        "ico",
        "im1",
        "im8",
        "im24",
        "j2c",
        "j2k",
        "jls",
        "jp2",
        "jpeg",
        "jpg",
        "jxl",
        "ljpg",
        "pam",
        "pbm",
        "pcx",
        "pfm",
        "pgm",
        "pgmyuv",
        "phm",
        "pix",
        "png",
        "ppm",
        "qoi",
        "ras",
        "rs",
        "sgi",
        "sun",
        "sunras",
        "tga",
        "tif",
        "tiff",
        "vbn",
        "wbmp",
        "webp",
        "xbm",
        "xface",
        "xwd",
    };

    public static readonly IReadOnlySet<string> AnimatedImageOutputs = new HashSet<string>(StringComparer.Ordinal)
    {
        "apng",
        "avif",
        "gif",
        "webp",
    };

    public static readonly IReadOnlySet<string> CadInputs = new HashSet<string>(StringComparer.Ordinal)
    {
        "dwg",
        "dxf",
        "dws",
        "dwt",
    };

    public static readonly IReadOnlySet<string> CadOutputs = new HashSet<string>(StringComparer.Ordinal)
    {
        "glb",
    };

    public static bool IsMediaConversion(string originalFormat, string convertedFormat) => originalFormat != convertedFormat && (AudioInputs.Contains(originalFormat)
        ? AudioOutputs.Contains(convertedFormat)
        : VideoInputs.Contains(originalFormat) && (AudioOutputs.Contains(convertedFormat) || VideoOutputs.Contains(convertedFormat)));

    public static bool IsImageConversion(string originalFormat, string convertedFormat) =>
        originalFormat != convertedFormat &&
        ImageInputs.Contains(originalFormat) &&
        ImageOutputs.Contains(convertedFormat);

    public static bool IsFfmpegConversion(string originalFormat, string convertedFormat) =>
        IsMediaConversion(originalFormat, convertedFormat) ||
        IsImageConversion(originalFormat, convertedFormat);

    public static bool IsCadConversion(string originalFormat, string convertedFormat) => CadInputs.Contains(originalFormat) && CadOutputs.Contains(convertedFormat);

    public static bool IsSupportedInput(string format) =>
        AudioInputs.Contains(format) ||
        VideoInputs.Contains(format) ||
        ImageInputs.Contains(format) ||
        CadInputs.Contains(format);

    public static bool IsSupportedOutput(string format) =>
        AudioOutputs.Contains(format) ||
        VideoOutputs.Contains(format) ||
        ImageOutputs.Contains(format) ||
        CadOutputs.Contains(format);

    public static bool IsSupportedConversion(string originalFormat, string convertedFormat) =>
        IsFfmpegConversion(originalFormat, convertedFormat) ||
        IsCadConversion(originalFormat, convertedFormat);
}
