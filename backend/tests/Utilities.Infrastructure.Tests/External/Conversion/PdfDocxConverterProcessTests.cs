using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Utilities.Infrastructure.Configuration;
using Utilities.Infrastructure.External.Conversion;
using Xunit;

namespace Utilities.Infrastructure.Tests.External.Conversion;

public sealed class PdfDocxConverterProcessTests
{
    [Fact]
    public async Task ConvertAsync_MissingExecutable_ReturnsUnavailable()
    {
        using var files = new TemporaryDirectory();
        var process = CreateProcess(files.GetPath($"missing-{Guid.NewGuid():N}.exe"));

        var result = await process.ConvertAsync(
            new PdfDocxConverterProcessRequest(
                files.WriteInput("source.pdf", "%PDF-1.7\n"),
                files.GetPath("output.docx")),
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.TimedOut);
        Assert.Equal(PdfDocxConverterExitCodes.Unavailable, result.ExitCode);
        Assert.Contains(
            "build-converter-workers.cmd -Worker PdfDocx",
            result.Diagnostics,
            StringComparison.Ordinal);
        Assert.Contains("requirements.lock", result.Diagnostics, StringComparison.Ordinal);
        Assert.Contains(
            "FileConversion:PdfDocxConverterExecutablePath",
            result.Diagnostics,
            StringComparison.Ordinal);
        Assert.Contains(
            "FileConversion:PdfDocxConverterScriptPath",
            result.Diagnostics,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertAsync_InvalidPdfHeader_DoesNotStartWorker()
    {
        using var files = new TemporaryDirectory();
        var process = CreateProcess(files.GetPath($"missing-{Guid.NewGuid():N}.exe"));

        var result = await process.ConvertAsync(
            new PdfDocxConverterProcessRequest(
                files.WriteInput("source.pdf", "not a pdf"),
                files.GetPath("output.docx")),
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PdfDocxConverterExitCodes.InvalidInput, result.ExitCode);
        Assert.Contains("PDF header", result.Diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertAsync_InputOverConfiguredLimit_DoesNotStartWorker()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteInput("source.pdf", "%PDF-1.7\n");
        var options = new FileConversionOptions
        {
            PdfDocxConverterExecutablePath = files.GetPath($"missing-{Guid.NewGuid():N}.exe"),
            PdfDocxMaxInputBytes = new FileInfo(inputPath).Length - 1,
        };
        var process = new PdfDocxConverterProcess(
            Options.Create(options),
            NullLogger<PdfDocxConverterProcess>.Instance);

        var result = await process.ConvertAsync(
            new PdfDocxConverterProcessRequest(inputPath, files.GetPath("output.docx")),
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(PdfDocxConverterExitCodes.ResourceLimitExceeded, result.ExitCode);
    }

    private static PdfDocxConverterProcess CreateProcess(string executablePath) =>
        new(
            Options.Create(new FileConversionOptions
            {
                PdfDocxConverterExecutablePath = executablePath,
                PdfDocxConversionTimeoutSeconds = 10,
            }),
            NullLogger<PdfDocxConverterProcess>.Instance);

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            "utilities-pdf-docx-process-tests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(_path);

        public string GetPath(string fileName) => Path.Combine(_path, fileName);

        public string WriteInput(string fileName, string content)
        {
            var path = GetPath(fileName);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, recursive: true);
            }
        }
    }
}
