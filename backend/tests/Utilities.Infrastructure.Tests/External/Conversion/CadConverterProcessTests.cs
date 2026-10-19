using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Utilities.Infrastructure.Configuration;
using Utilities.Infrastructure.External.Conversion;
using Xunit;

namespace Utilities.Infrastructure.Tests.External.Conversion;

public sealed class CadConverterProcessTests
{
    [Fact]
    public async Task ConvertAsync_MissingExecutable_ReturnsUnavailable()
    {
        using var files = new TemporaryDirectory();
        var process = CreateProcess(
            files.GetPath($"missing-{Guid.NewGuid():N}.exe"),
            timeoutSeconds: 10);

        var result = await process.ConvertAsync(
            CreateRequest(files.WriteInput("source.dwg", "progress"), files.GetPath("output.glb")),
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.TimedOut);
        Assert.Equal(CadConverterExitCodes.Unavailable, result.ExitCode);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public async Task ConvertAsync_RealProcess_ParsesProgressAndDoesNotForwardSecrets()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteInput("source.dwg", "progress");
        var progress = new List<double>();
        var previousSecret = Environment.GetEnvironmentVariable("CAD_PROCESS_FIXTURE_SECRET");
        Environment.SetEnvironmentVariable("CAD_PROCESS_FIXTURE_SECRET", "must-not-leak");

        try
        {
            var result = await CreateProcess(FixturePath, timeoutSeconds: 10).ConvertAsync(
                CreateRequest(inputPath, files.GetPath("output.glb")),
                progress.Add,
                CancellationToken.None);

            Assert.True(result.Success, result.Diagnostics);
            Assert.Equal([10, 100], progress);
            Assert.Contains("[stdout] []", result.Diagnostics, StringComparison.Ordinal);
            Assert.Contains("secret-present=False", result.Diagnostics, StringComparison.Ordinal);
            Assert.DoesNotContain("must-not-leak", result.Diagnostics, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CAD_PROCESS_FIXTURE_SECRET", previousSecret);
        }
    }

    [Fact]
    public async Task ConvertAsync_DiagnosticsAreBounded()
    {
        using var files = new TemporaryDirectory();
        var result = await CreateProcess(FixturePath, timeoutSeconds: 10).ConvertAsync(
            CreateRequest(
                files.WriteInput("source.dwg", "spam"),
                files.GetPath("output.glb")),
            _ => { },
            CancellationToken.None);

        Assert.True(result.Success, result.Diagnostics);
        Assert.InRange(result.Diagnostics.Length, 1, 32 * 1024);
        Assert.StartsWith("[stderr] ", result.Diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertAsync_ForwardsConfiguredResourceLimitsExactly()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteInput("source.dwg", "arguments");
        var process = CreateProcess(
            FixturePath,
            timeoutSeconds: 10,
            options =>
            {
                options.CadMaxInputBytes = 12_345;
                options.CadMaxEntities = 23_456;
                options.CadMaxVertices = 34_567;
                options.CadMaxTriangles = 45_678;
                options.CadMaxLineSegments = 56_789;
                options.CadMaxOutputBytes = 67_890;
            });

        var result = await process.ConvertAsync(
            CreateRequest(inputPath, files.GetPath("output.glb")),
            _ => { },
            CancellationToken.None);

        Assert.True(result.Success, result.Diagnostics);
        Assert.Contains("[stderr] --max-input-bytes=12345", result.Diagnostics, StringComparison.Ordinal);
        Assert.Contains("[stderr] --max-entities=23456", result.Diagnostics, StringComparison.Ordinal);
        Assert.Contains("[stderr] --max-vertices=34567", result.Diagnostics, StringComparison.Ordinal);
        Assert.Contains("[stderr] --max-triangles=45678", result.Diagnostics, StringComparison.Ordinal);
        Assert.Contains("[stderr] --max-line-segments=56789", result.Diagnostics, StringComparison.Ordinal);
        Assert.Contains("[stderr] --max-output-bytes=67890", result.Diagnostics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertAsync_InputOverConfiguredLimit_DoesNotStartWorker()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteInput("source.dwg", "progress");
        var inputLength = new FileInfo(inputPath).Length;
        var process = CreateProcess(
            files.GetPath($"missing-{Guid.NewGuid():N}.exe"),
            timeoutSeconds: 10,
            options => options.CadMaxInputBytes = inputLength - 1);

        var result = await process.ConvertAsync(
            CreateRequest(inputPath, files.GetPath("output.glb")),
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.TimedOut);
        Assert.Equal(CadConverterExitCodes.ResourceLimitExceeded, result.ExitCode);
        Assert.Contains("exceeding", result.Diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertAsync_InputAtConfiguredLimit_StartsWorker()
    {
        using var files = new TemporaryDirectory();
        var inputPath = files.WriteInput("source.dwg", "progress");
        var inputLength = new FileInfo(inputPath).Length;
        var process = CreateProcess(
            FixturePath,
            timeoutSeconds: 10,
            options => options.CadMaxInputBytes = inputLength);

        var result = await process.ConvertAsync(
            CreateRequest(inputPath, files.GetPath("output.glb")),
            _ => { },
            CancellationToken.None);

        Assert.True(result.Success, result.Diagnostics);
    }

    [Fact]
    public async Task ConvertAsync_Timeout_KillsWorkerAndReturnsTimedOut()
    {
        using var files = new TemporaryDirectory();
        var stopwatch = Stopwatch.StartNew();

        var result = await CreateProcess(FixturePath, timeoutSeconds: 1).ConvertAsync(
            CreateRequest(
                files.WriteInput("source.dwg", "sleep"),
                files.GetPath("output.glb")),
            _ => { },
            CancellationToken.None);

        stopwatch.Stop();
        Assert.False(result.Success);
        Assert.True(result.TimedOut);
        Assert.Equal(CadConverterExitCodes.InternalError, result.ExitCode);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(8));
    }

    [Fact]
    public async Task ConvertAsync_Cancellation_KillsWorkerAndRethrowsCancellation()
    {
        using var files = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateProcess(FixturePath, timeoutSeconds: 30).ConvertAsync(
                CreateRequest(
                    files.WriteInput("source.dwg", "sleep"),
                    files.GetPath("output.glb")),
                _ => { },
                cancellation.Token));
    }

    [Fact]
    public async Task ConvertAsync_UnsupportedFormat_DoesNotStartWorker()
    {
        using var files = new TemporaryDirectory();
        var result = await CreateProcess(FixturePath, timeoutSeconds: 10).ConvertAsync(
            new CadConverterProcessRequest(
                files.WriteInput("source.txt", "progress"),
                files.GetPath("output.glb"),
                "txt"),
            _ => { },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CadConverterExitCodes.InvalidInput, result.ExitCode);
    }

    private static string FixturePath
    {
        get
        {
#if DEBUG
            const string configuration = "Debug";
#else
            const string configuration = "Release";
#endif
            var executableName = OperatingSystem.IsWindows()
                ? "Utilities.CadProcessFixture.exe"
                : "Utilities.CadProcessFixture";
            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "Utilities.CadProcessFixture",
                "bin",
                configuration,
                "net10.0",
                executableName));
        }
    }

    private static CadConverterProcess CreateProcess(
        string executablePath,
        int timeoutSeconds,
        Action<FileConversionOptions>? configure = null)
    {
        var options = new FileConversionOptions
        {
            CadConverterExecutablePath = executablePath,
            CadConversionTimeoutSeconds = timeoutSeconds,
        };
        configure?.Invoke(options);

        return new CadConverterProcess(
            Options.Create(options),
            NullLogger<CadConverterProcess>.Instance);
    }

    private static CadConverterProcessRequest CreateRequest(
        string inputPath,
        string outputPath) => new(inputPath, outputPath, "dwg");

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            "utilities-cad-process-tests",
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
