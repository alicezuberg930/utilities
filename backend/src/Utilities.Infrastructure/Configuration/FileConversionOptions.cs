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

    public string CadConverterExecutablePath { get; set; } = OperatingSystem.IsWindows()
        ? "utilities-cad-converter.exe"
        : "utilities-cad-converter";

    public int CadConversionTimeoutSeconds { get; set; } = 900;
    public double CadLinearDeflectionMeters { get; set; } = 0.0005;
    public double CadAngularDeflectionDegrees { get; set; } = 10;
    public bool CadIncludeHiddenObjects { get; set; } = true;
    public bool CadExportEdgeWires { get; set; } = true;
    public bool CadRequireCompleteGeometry { get; set; } = true;
    public long CadMaxInputBytes { get; set; } = 256L * 1024 * 1024;
    public long CadMaxEntities { get; set; } = 250_000;
    public long CadMaxVertices { get; set; } = 5_000_000;
    public long CadMaxTriangles { get; set; } = 2_000_000;
    public long CadMaxLineSegments { get; set; } = 2_000_000;
    public long CadMaxOutputBytes { get; set; } = 256L * 1024 * 1024;
}
