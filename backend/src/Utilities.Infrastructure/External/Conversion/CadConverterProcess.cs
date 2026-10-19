using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utilities.Infrastructure.Configuration;

namespace Utilities.Infrastructure.External.Conversion;

public sealed class CadConverterProcess(
    IOptions<FileConversionOptions> options,
    ILogger<CadConverterProcess> logger) : ICadConverterProcess
{
    private const int MaximumDiagnosticCharacters = 32 * 1024;
    private static readonly TimeSpan ShutdownDrainTimeout = TimeSpan.FromSeconds(5);
    private static readonly IReadOnlySet<string> SupportedInputFormats =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "dwg",
            "dxf",
            "dws",
            "dwt",
        };
    private static readonly IReadOnlySet<string> ForwardedEnvironmentVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PATH",
            "PATHEXT",
            "SYSTEMROOT",
            "WINDIR",
            "TEMP",
            "TMP",
            "TMPDIR",
            "HOME",
            "USERPROFILE",
            "LD_LIBRARY_PATH",
            "DYLD_LIBRARY_PATH",
            "LANG",
            "DOTNET_ROOT",
            "DOTNET_ROOT_X64",
        };
    private readonly FileConversionOptions _options = options.Value;

    public async Task<CadConverterProcessResult> ConvertAsync(
        CadConverterProcessRequest request,
        Action<double> reportProgress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(reportProgress);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new BoundedDiagnostics(MaximumDiagnosticCharacters);
        if (string.IsNullOrWhiteSpace(request.InputPath) ||
            string.IsNullOrWhiteSpace(request.OutputPath) ||
            !SupportedInputFormats.Contains(request.InputFormat))
        {
            return new CadConverterProcessResult(
                CadConverterExitCodes.InvalidInput,
                TimedOut: false,
                "The CAD converter request is invalid.");
        }

        try
        {
            var inputFile = new FileInfo(Path.GetFullPath(request.InputPath));
            if (!inputFile.Exists)
            {
                return new CadConverterProcessResult(
                    CadConverterExitCodes.InvalidInput,
                    TimedOut: false,
                    "The CAD input file does not exist.");
            }

            if (inputFile.Length > _options.CadMaxInputBytes)
            {
                return new CadConverterProcessResult(
                    CadConverterExitCodes.ResourceLimitExceeded,
                    TimedOut: false,
                    $"The CAD input is {inputFile.Length} bytes, exceeding the configured " +
                    $"limit of {_options.CadMaxInputBytes} bytes.");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or
                                          IOException or
                                          NotSupportedException or
                                          PathTooLongException or
                                          SecurityException or
                                          UnauthorizedAccessException)
        {
            return new CadConverterProcessResult(
                CadConverterExitCodes.InvalidInput,
                TimedOut: false,
                exception.Message);
        }

        ProcessStartInfo startInfo;
        try
        {
            startInfo = CreateStartInfo(request);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                          NotSupportedException or
                                          PathTooLongException or
                                          SecurityException)
        {
            return new CadConverterProcessResult(
                CadConverterExitCodes.InvalidInput,
                TimedOut: false,
                exception.Message);
        }

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        try
        {
            if (!process.Start())
            {
                return new CadConverterProcessResult(
                    CadConverterExitCodes.Unavailable,
                    TimedOut: false,
                    "The CAD converter process could not be started.");
            }
        }
        catch (Exception exception) when (exception is Win32Exception or
                                          FileNotFoundException or
                                          UnauthorizedAccessException or
                                          InvalidOperationException or
                                          ArgumentException or
                                          SecurityException)
        {
            logger.LogWarning(
                exception,
                "Could not start the CAD converter executable {ExecutablePath}",
                _options.CadConverterExecutablePath);
            return new CadConverterProcessResult(
                CadConverterExitCodes.Unavailable,
                TimedOut: false,
                exception.Message);
        }

        var stdoutTask = PumpStandardOutputAsync(
            process.StandardOutput,
            reportProgress,
            diagnostics);
        var stderrTask = PumpDiagnosticsAsync(process.StandardError, "stderr", diagnostics);

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(Math.Clamp(_options.CadConversionTimeoutSeconds, 1, 7_200)));
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        try
        {
            await process.WaitForExitAsync(linkedCancellation.Token);
            await Task.WhenAll(stdoutTask, stderrTask).WaitAsync(linkedCancellation.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKillProcessTree(process);
            await DrainAfterTerminationAsync(process, stdoutTask, stderrTask);
            throw;
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            await DrainAfterTerminationAsync(process, stdoutTask, stderrTask);
            return new CadConverterProcessResult(
                CadConverterExitCodes.InternalError,
                TimedOut: true,
                diagnostics.ToString());
        }
        catch (Exception exception) when (exception is IOException or
                                          InvalidOperationException or
                                          ObjectDisposedException)
        {
            TryKillProcessTree(process);
            await DrainAfterTerminationAsync(process, stdoutTask, stderrTask);
            diagnostics.Append("host", exception.Message);
            logger.LogWarning(exception, "The CAD converter process failed unexpectedly");
            return new CadConverterProcessResult(
                CadConverterExitCodes.InternalError,
                TimedOut: false,
                diagnostics.ToString());
        }

        return new CadConverterProcessResult(
            process.ExitCode,
            TimedOut: false,
            diagnostics.ToString());
    }

    private ProcessStartInfo CreateStartInfo(CadConverterProcessRequest request)
    {
        var inputPath = Path.GetFullPath(request.InputPath);
        var outputPath = Path.GetFullPath(request.OutputPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.CadConverterExecutablePath,
            // Never make an untrusted upload directory the child's current
            // directory. Native dependency/config lookup can consult it on
            // some platforms, while the worker already receives absolute
            // input and output paths.
            WorkingDirectory = GetSafeWorkingDirectory(_options.CadConverterExecutablePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        CopySafeEnvironment(startInfo);

        startInfo.ArgumentList.Add("--input");
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add(request.InputFormat);
        startInfo.ArgumentList.Add("--linear-deflection-meters");
        startInfo.ArgumentList.Add(_options.CadLinearDeflectionMeters.ToString("R", CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--angular-deflection-degrees");
        startInfo.ArgumentList.Add(_options.CadAngularDeflectionDegrees.ToString("R", CultureInfo.InvariantCulture));
        AddNumericArgument(startInfo, "--max-input-bytes", _options.CadMaxInputBytes);
        AddNumericArgument(startInfo, "--max-entities", _options.CadMaxEntities);
        AddNumericArgument(startInfo, "--max-vertices", _options.CadMaxVertices);
        AddNumericArgument(startInfo, "--max-triangles", _options.CadMaxTriangles);
        AddNumericArgument(startInfo, "--max-line-segments", _options.CadMaxLineSegments);
        AddNumericArgument(startInfo, "--max-output-bytes", _options.CadMaxOutputBytes);

        if (_options.CadIncludeHiddenObjects)
        {
            startInfo.ArgumentList.Add("--include-hidden");
        }

        if (_options.CadExportEdgeWires)
        {
            startInfo.ArgumentList.Add("--export-edge-wires");
        }

        if (_options.CadRequireCompleteGeometry)
        {
            startInfo.ArgumentList.Add("--require-complete-geometry");
        }

        return startInfo;
    }

    private static void AddNumericArgument(
        ProcessStartInfo startInfo,
        string name,
        long value)
    {
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(value.ToString(CultureInfo.InvariantCulture));
    }

    private static string GetSafeWorkingDirectory(string executablePath)
    {
        if (Path.IsPathFullyQualified(executablePath))
        {
            return Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;
        }

        return AppContext.BaseDirectory;
    }

    private async Task PumpStandardOutputAsync(
        StreamReader reader,
        Action<double> reportProgress,
        BoundedDiagnostics diagnostics)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            if (!TryReadProgress(line, out var progress))
            {
                diagnostics.Append("stdout", line);
                continue;
            }

            try
            {
                reportProgress(Math.Clamp(progress, 0, 100));
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "The CAD conversion progress callback failed");
            }
        }
    }

    private static async Task PumpDiagnosticsAsync(
        StreamReader reader,
        string source,
        BoundedDiagnostics diagnostics)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            diagnostics.Append(source, line);
        }
    }

    private static bool TryReadProgress(string line, out double progress)
    {
        progress = 0;
        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("progress", out var value) &&
                   value.TryGetDouble(out progress) &&
                   double.IsFinite(progress);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void CopySafeEnvironment(ProcessStartInfo startInfo)
    {
        startInfo.Environment.Clear();
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            if (variable.Key is not string key ||
                variable.Value is not string value ||
                (!ForwardedEnvironmentVariables.Contains(key) &&
                 !key.StartsWith("LC_", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            startInfo.Environment[key] = value;
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                          Win32Exception or
                                          NotSupportedException)
        {
            // The process may have exited between the check and the kill request.
        }
    }

    private static async Task DrainAfterTerminationAsync(
        Process process,
        Task stdoutTask,
        Task stderrTask)
    {
        try
        {
            await Task.WhenAll(
                    process.WaitForExitAsync(CancellationToken.None),
                    stdoutTask,
                    stderrTask)
                .WaitAsync(ShutdownDrainTimeout);
        }
        catch (Exception exception) when (exception is TimeoutException or
                                          InvalidOperationException or
                                          IOException or
                                          ObjectDisposedException)
        {
            // Cancellation and timeout should not be delayed by a stuck child stream.
        }
    }

    private sealed class BoundedDiagnostics(int maximumCharacters)
    {
        private readonly StringBuilder _builder = new();
        private readonly object _gate = new();

        public void Append(string source, string value)
        {
            lock (_gate)
            {
                var remaining = maximumCharacters - _builder.Length;
                if (remaining <= 0)
                {
                    return;
                }

                var prefix = $"[{source}] ";
                _builder.Append(prefix.AsSpan(0, Math.Min(prefix.Length, remaining)));
                remaining = maximumCharacters - _builder.Length;
                if (remaining > 0)
                {
                    _builder.Append(value.AsSpan(0, Math.Min(value.Length, remaining)));
                }

                if (_builder.Length < maximumCharacters)
                {
                    _builder.AppendLine();
                }
            }
        }

        public override string ToString()
        {
            lock (_gate)
            {
                return _builder.ToString().TrimEnd();
            }
        }
    }
}
