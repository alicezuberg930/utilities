using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Utilities.Domain.Entities;
using Utilities.Infrastructure.Configuration;
using Utilities.Infrastructure.External.Conversion;
using Xunit;

namespace Utilities.Infrastructure.Tests.External.Conversion;

public sealed class PdfDocxFileConversionEngineTests
{
    [Fact]
    public async Task ConvertAsync_SuccessfulWorker_PublishesValidatedOutput()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteText("report.pdf", "%PDF-1.7");
        var outputPath = files.GetPath("report.docx");
        PdfDocxConverterProcessRequest? receivedRequest = null;
        var progress = new List<double>();
        var process = new FakePdfDocxConverterProcess((request, reportProgress, _) =>
        {
            receivedRequest = request;
            reportProgress(40);
            DocxValidatorTests.WriteDocx(request.OutputPath);
            return Task.FromResult(Success());
        });

        var result = await CreateEngine(process).ConvertAsync(
            CreateTask(),
            inputPath,
            outputPath,
            progress.Add,
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(outputPath));
        await DocxValidator.ValidateAsync(outputPath, CancellationToken.None);
        Assert.NotNull(receivedRequest);
        Assert.Equal(inputPath, receivedRequest.InputPath);
        Assert.NotEqual(outputPath, receivedRequest.OutputPath);
        Assert.EndsWith(".partial.docx", receivedRequest.OutputPath, StringComparison.Ordinal);
        Assert.False(File.Exists(receivedRequest.OutputPath));
        Assert.Contains(progress, value => Math.Abs(value - 41) < 0.000001);
        Assert.Equal(99, progress[^1]);
    }

    [Fact]
    public async Task ConvertAsync_FailedWorker_MapsFailureAndDeletesPartialOutput()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteText("broken.pdf", "%PDF-1.7");
        var outputPath = files.GetPath("broken.docx");
        string? partialOutputPath = null;
        var process = new FakePdfDocxConverterProcess((request, _, _) =>
        {
            partialOutputPath = request.OutputPath;
            File.WriteAllText(request.OutputPath, "partial");
            return Task.FromResult(new PdfDocxConverterProcessResult(
                PdfDocxConverterExitCodes.ReadFailed,
                TimedOut: false,
                "reader failed"));
        });

        var result = await CreateEngine(process).ConvertAsync(
            CreateTask(),
            inputPath,
            outputPath,
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("could not be read", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outputPath));
        Assert.NotNull(partialOutputPath);
        Assert.False(File.Exists(partialOutputPath));
    }

    [Fact]
    public async Task ConvertAsync_MalformedDocx_ReturnsFailureAndDeletesPartialOutput()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteText("broken.pdf", "%PDF-1.7");
        var outputPath = files.GetPath("broken.docx");
        string? partialOutputPath = null;
        var process = new FakePdfDocxConverterProcess((request, _, _) =>
        {
            partialOutputPath = request.OutputPath;
            File.WriteAllText(request.OutputPath, "not a docx");
            return Task.FromResult(Success());
        });

        var result = await CreateEngine(process).ConvertAsync(
            CreateTask(),
            inputPath,
            outputPath,
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("invalid or empty DOCX", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outputPath));
        Assert.NotNull(partialOutputPath);
        Assert.False(File.Exists(partialOutputPath));
    }

    [Theory]
    [InlineData(PdfDocxConverterExitCodes.InvalidInput, false, "invalid")]
    [InlineData(PdfDocxConverterExitCodes.Unavailable, false, "not available")]
    [InlineData(PdfDocxConverterExitCodes.ReadFailed, false, "could not be read")]
    [InlineData(PdfDocxConverterExitCodes.WriteFailed, false, "generated or written")]
    [InlineData(PdfDocxConverterExitCodes.ResourceLimitExceeded, false, "page-count")]
    [InlineData(PdfDocxConverterExitCodes.InternalError, false, "unexpectedly")]
    [InlineData(PdfDocxConverterExitCodes.InternalError, true, "time limit")]
    public async Task ConvertAsync_WorkerFailure_ReturnsStablePublicError(
        int exitCode,
        bool timedOut,
        string expectedMessage)
    {
        using var files = new TemporaryDirectory();
        var process = new FakePdfDocxConverterProcess((_, _, _) =>
            Task.FromResult(new PdfDocxConverterProcessResult(
                exitCode,
                timedOut,
                "internal details")));

        var result = await CreateEngine(process).ConvertAsync(
            CreateTask(),
            files.WriteText("source.pdf", "%PDF-1.7"),
            files.GetPath("output.docx"),
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(expectedMessage, result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal details", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pdf", "docx", true)]
    [InlineData("PDF", "docx", false)]
    [InlineData("pdf", "pdf", false)]
    [InlineData("docx", "pdf", false)]
    public void CanConvert_UsesDocumentConversionMatrix(
        string inputFormat,
        string outputFormat,
        bool expected)
    {
        var engine = CreateEngine(new FakePdfDocxConverterProcess((_, _, _) =>
            Task.FromResult(Success())));

        Assert.Equal(expected, engine.CanConvert(inputFormat, outputFormat));
    }

    private static PdfDocxFileConversionEngine CreateEngine(
        IPdfDocxConverterProcess process,
        long maxOutputBytes = 256L * 1024 * 1024) =>
        new(
            process,
            Options.Create(new FileConversionOptions
            {
                PdfDocxMaxOutputBytes = maxOutputBytes,
            }),
            NullLogger<PdfDocxFileConversionEngine>.Instance);

    private static ConvertTask CreateTask()
    {
        var jobId = Guid.NewGuid();
        return new ConvertTask
        {
            JobId = jobId,
            Job = new Job { JobId = jobId },
            OriginalFormat = "pdf",
            ConvertedFormat = "docx",
            DownloadUrl = $"/api/v1/convert/file/{jobId}",
        };
    }

    private static PdfDocxConverterProcessResult Success() =>
        new(PdfDocxConverterExitCodes.Success, TimedOut: false, string.Empty);

    private sealed class FakePdfDocxConverterProcess(
        Func<PdfDocxConverterProcessRequest, Action<double>, CancellationToken, Task<PdfDocxConverterProcessResult>> convert) : IPdfDocxConverterProcess
    {
        public Task<PdfDocxConverterProcessResult> ConvertAsync(
            PdfDocxConverterProcessRequest request,
            Action<double> reportProgress,
            CancellationToken cancellationToken) =>
            convert(request, reportProgress, cancellationToken);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            "utilities-pdf-docx-engine-tests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(_path);

        public string GetPath(string fileName) => Path.Combine(_path, fileName);

        public string WriteText(string fileName, string content)
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
