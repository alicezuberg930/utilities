using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Utilities.Domain.Entities;
using Utilities.Infrastructure.Configuration;
using Utilities.Infrastructure.External.Conversion;
using Xunit;

namespace Utilities.Infrastructure.Tests.External.Conversion;

public sealed class CadFileConversionEngineTests
{
    [Fact]
    public async Task ConvertAsync_SuccessfulWorker_PublishesValidatedOutput()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteText("house.dwg", "source");
        var outputPath = files.GetPath("house.glb");
        CadConverterProcessRequest? receivedRequest = null;
        var progress = new List<double>();
        var process = new FakeCadConverterProcess((request, reportProgress, _) =>
        {
            receivedRequest = request;
            reportProgress(42);
            WriteMinimalGlb(request.OutputPath);
            return Task.FromResult(Success());
        });

        var result = await CreateEngine(process).ConvertAsync(
            CreateTask("dwg"),
            inputPath,
            outputPath,
            progress.Add,
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(outputPath));
        await GlbValidator.ValidateAsync(outputPath, CancellationToken.None);
        Assert.NotNull(receivedRequest);
        Assert.Equal(inputPath, receivedRequest.InputPath);
        Assert.Equal("dwg", receivedRequest.InputFormat);
        Assert.NotEqual(outputPath, receivedRequest.OutputPath);
        Assert.EndsWith(".partial.glb", receivedRequest.OutputPath, StringComparison.Ordinal);
        Assert.False(File.Exists(receivedRequest.OutputPath));
        Assert.Contains(progress, value => Math.Abs(value - 42.8) < 0.000001);
        Assert.Equal(99, progress[^1]);
    }

    [Fact]
    public async Task ConvertAsync_FailedWorker_MapsExitCodeAndDeletesPartialOutput()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteText("broken.dwg", "source");
        var outputPath = files.GetPath("broken.glb");
        string? partialOutputPath = null;
        var process = new FakeCadConverterProcess((request, _, _) =>
        {
            partialOutputPath = request.OutputPath;
            File.WriteAllText(request.OutputPath, "partial");
            return Task.FromResult(new CadConverterProcessResult(
                CadConverterExitCodes.ImportFailed,
                TimedOut: false,
                "reader failed"));
        });

        var result = await CreateEngine(process).ConvertAsync(
            CreateTask("dwg"),
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
    public async Task ConvertAsync_MalformedGlb_ReturnsFailureAndDeletesPartialOutput()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteText("broken.dxf", "source");
        var outputPath = files.GetPath("broken.glb");
        string? partialOutputPath = null;
        var process = new FakeCadConverterProcess((request, _, _) =>
        {
            partialOutputPath = request.OutputPath;
            File.WriteAllBytes(request.OutputPath, Encoding.UTF8.GetBytes("not a GLB"));
            return Task.FromResult(Success());
        });

        var result = await CreateEngine(process).ConvertAsync(
            CreateTask("dxf"),
            inputPath,
            outputPath,
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("invalid or empty GLB", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outputPath));
        Assert.NotNull(partialOutputPath);
        Assert.False(File.Exists(partialOutputPath));
    }

    [Fact]
    public async Task ConvertAsync_SuccessWithoutOutput_ReturnsFailure()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteText("empty.dwg", "source");
        var outputPath = files.GetPath("empty.glb");
        var process = new FakeCadConverterProcess((_, _, _) => Task.FromResult(Success()));

        var result = await CreateEngine(process).ConvertAsync(
            CreateTask("dwg"),
            inputPath,
            outputPath,
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("did not produce", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task ConvertAsync_OutputOverConfiguredLimit_FailsBeforeValidationAndDeletesPartialOutput()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteText("house.dwg", "source");
        var outputPath = files.GetPath("house.glb");
        string? partialOutputPath = null;
        var process = new FakeCadConverterProcess((request, _, _) =>
        {
            partialOutputPath = request.OutputPath;
            File.WriteAllBytes(request.OutputPath, new byte[65]);
            return Task.FromResult(Success());
        });

        var result = await CreateEngine(process, maxOutputBytes: 64).ConvertAsync(
            CreateTask("dwg"),
            inputPath,
            outputPath,
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("complexity or output-size limit", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invalid or empty GLB", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outputPath));
        Assert.NotNull(partialOutputPath);
        Assert.False(File.Exists(partialOutputPath));
    }

    [Theory]
    [InlineData(CadConverterExitCodes.InvalidInput, false, "invalid")]
    [InlineData(CadConverterExitCodes.Unavailable, false, "not available")]
    [InlineData(CadConverterExitCodes.ExportFailed, false, "tessellated")]
    [InlineData(CadConverterExitCodes.ResourceLimitExceeded, false, "complexity or output-size limit")]
    [InlineData(CadConverterExitCodes.InternalError, false, "unexpectedly")]
    [InlineData(CadConverterExitCodes.InternalError, true, "time limit")]
    public async Task ConvertAsync_WorkerFailure_ReturnsStablePublicError(
        int exitCode,
        bool timedOut,
        string expectedMessage)
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteText("source.dwg", "source");
        var outputPath = files.GetPath("output.glb");
        var process = new FakeCadConverterProcess((_, _, _) =>
            Task.FromResult(new CadConverterProcessResult(exitCode, timedOut, string.Empty)));

        var result = await CreateEngine(process).ConvertAsync(
            CreateTask("dwg"),
            inputPath,
            outputPath,
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(expectedMessage, result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertAsync_PreCancelled_DoesNotStartWorkerOrWriteOutput()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteText("house.dwg", "source");
        var outputPath = files.GetPath("house.glb");
        var invoked = false;
        var process = new FakeCadConverterProcess((_, _, _) =>
        {
            invoked = true;
            return Task.FromResult(Success());
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateEngine(process).ConvertAsync(
                CreateTask("dwg"),
                inputPath,
                outputPath,
                _ => { },
                cancellation.Token));

        Assert.False(invoked);
        Assert.False(File.Exists(outputPath));
    }

    [Theory]
    [InlineData("dwg", "glb", true)]
    [InlineData("dxf", "glb", true)]
    [InlineData("dws", "glb", true)]
    [InlineData("dwt", "glb", true)]
    [InlineData("dxf", "mp4", false)]
    [InlineData("mp4", "glb", false)]
    public void CanConvert_UsesCadConversionMatrix(
        string inputFormat,
        string outputFormat,
        bool expected)
    {
        var engine = CreateEngine(new FakeCadConverterProcess((_, _, _) =>
            Task.FromResult(Success())));

        Assert.Equal(expected, engine.CanConvert(inputFormat, outputFormat));
    }

    private static CadFileConversionEngine CreateEngine(
        ICadConverterProcess process,
        long maxOutputBytes = 256L * 1024 * 1024) =>
        new(
            process,
            Options.Create(new FileConversionOptions
            {
                CadMaxOutputBytes = maxOutputBytes,
            }),
            NullLogger<CadFileConversionEngine>.Instance);

    private static CadConverterProcessResult Success() => new(
        CadConverterExitCodes.Success,
        TimedOut: false,
        string.Empty);

    private static ConvertTask CreateTask(string originalFormat) => new()
    {
        JobId = Guid.CreateVersion7(),
        OriginalFormat = originalFormat,
        ConvertedFormat = "glb",
        DownloadUrl = "/unused",
    };

    private static void WriteMinimalGlb(string path)
    {
        const string json =
            "{\"asset\":{\"version\":\"2.0\"}," +
            "\"scene\":0," +
            "\"scenes\":[{\"nodes\":[0]}]," +
            "\"nodes\":[{\"mesh\":0}]," +
            "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0}}]}]," +
            "\"buffers\":[{\"byteLength\":36}]," +
            "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":36}]," +
            "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\"}]}";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var paddedJsonLength = (jsonBytes.Length + 3) & ~3;
        var binary = new byte[36];
        BinaryPrimitives.WriteSingleLittleEndian(binary.AsSpan(12, 4), 1);
        BinaryPrimitives.WriteSingleLittleEndian(binary.AsSpan(28, 4), 1);
        var bytes = new byte[12 + 8 + paddedJsonLength + 8 + binary.Length];

        Encoding.ASCII.GetBytes("glTF").CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), (uint)bytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12, 4), (uint)paddedJsonLength);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), 0x4E4F534A);
        jsonBytes.CopyTo(bytes, 20);
        bytes.AsSpan(20 + jsonBytes.Length, paddedJsonLength - jsonBytes.Length).Fill(0x20);
        var binaryHeaderOffset = 20 + paddedJsonLength;
        BinaryPrimitives.WriteUInt32LittleEndian(
            bytes.AsSpan(binaryHeaderOffset, 4),
            (uint)binary.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(
            bytes.AsSpan(binaryHeaderOffset + 4, 4),
            0x004E4942);
        binary.CopyTo(bytes, binaryHeaderOffset + 8);
        File.WriteAllBytes(path, bytes);
    }

    private sealed class FakeCadConverterProcess(
        Func<CadConverterProcessRequest, Action<double>, CancellationToken, Task<CadConverterProcessResult>> handler)
        : ICadConverterProcess
    {
        public Task<CadConverterProcessResult> ConvertAsync(
            CadConverterProcessRequest request,
            Action<double> reportProgress,
            CancellationToken cancellationToken) =>
            handler(request, reportProgress, cancellationToken);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            "utilities-cad-tests",
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
