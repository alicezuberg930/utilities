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

public sealed class PdfDocxConverterProcess(
    IOptions<FileConversionOptions> options,
    ILogger<PdfDocxConverterProcess> logger) : IPdfDocxConverterProcess
{
    private const int MaximumDiagnosticCharacters = 32 * 1024;
    private const int PdfHeaderSearchBytes = 1024;
    private static readonly byte[] PdfHeader = "%PDF-"u8.ToArray();
    private static readonly TimeSpan ShutdownDrainTimeout = TimeSpan.FromSeconds(5);
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

    public async Task<PdfDocxConverterProcessResult> ConvertAsync(
        PdfDocxConverterProcessRequest request,
        Action<double> reportProgress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(reportProgress);
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new BoundedDiagnostics(MaximumDiagnosticCharacters);
        if (string.IsNullOrWhiteSpace(request.InputPath) ||
            string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return InvalidInput("The PDF to DOCX converter request is invalid.");
        }

        try
        {
            var inputFile = new FileInfo(Path.GetFullPath(request.InputPath));
            if (!inputFile.Exists)
            {
                return InvalidInput("The PDF input file does not exist.");
            }

            if (inputFile.Length > _options.PdfDocxMaxInputBytes)
            {
                return new PdfDocxConverterProcessResult(
                    PdfDocxConverterExitCodes.ResourceLimitExceeded,
                    TimedOut: false,
                    $"The PDF input is {inputFile.Length} bytes, exceeding the configured " +
                    $"limit of {_options.PdfDocxMaxInputBytes} bytes.");
            }

            if (!await HasPdfHeaderAsync(inputFile.FullName, cancellationToken))
            {
                return InvalidInput("The input does not contain a valid PDF header.");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or
                                          IOException or
                                          NotSupportedException or
                                          PathTooLongException or
                                          SecurityException or
                                          UnauthorizedAccessException)
        {
            return InvalidInput(exception.Message);
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
            return InvalidInput(exception.Message);
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
                return new PdfDocxConverterProcessResult(
                    PdfDocxConverterExitCodes.Unavailable,
                    TimedOut: false,
                    "The PDF to DOCX converter process could not be started.");
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
                "Could not start the PDF to DOCX converter executable {ExecutablePath}",
                startInfo.FileName);
            return new PdfDocxConverterProcessResult(
                PdfDocxConverterExitCodes.Unavailable,
                TimedOut: false,
                GetStartFailureDiagnostics(startInfo.FileName, exception));
        }

        var stdoutTask = PumpStandardOutputAsync(
            process.StandardOutput,
            reportProgress,
            diagnostics);
        var stderrTask = PumpDiagnosticsAsync(process.StandardError, "stderr", diagnostics);

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(Math.Clamp(_options.PdfDocxConversionTimeoutSeconds, 1, 7_200)));
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
            return new PdfDocxConverterProcessResult(
                PdfDocxConverterExitCodes.InternalError,
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
            logger.LogWarning(exception, "The PDF to DOCX converter process failed unexpectedly");
            return new PdfDocxConverterProcessResult(
                PdfDocxConverterExitCodes.InternalError,
                TimedOut: false,
                diagnostics.ToString());
        }

        return new PdfDocxConverterProcessResult(
            process.ExitCode,
            TimedOut: false,
            diagnostics.ToString());
    }

    private PdfDocxConverterProcessResult InvalidInput(string diagnostics) =>
        new(PdfDocxConverterExitCodes.InvalidInput, TimedOut: false, diagnostics);

    private static string GetStartFailureDiagnostics(
        string executablePath,
        Exception exception)
    {
        var guidance =
            $"The PDF to DOCX converter executable '{executablePath}' could not be started. " +
            "For local development on Windows, run " +
            "'.\\backend\\scripts\\build-converter-workers.cmd -Worker PdfDocx' " +
            "from the repository root. On other platforms, create a Python virtual " +
            "environment and install " +
            "'backend/vendor/pdf-docx-converter-worker/requirements.lock'. " +
            "Alternatively, configure FileConversion:PdfDocxConverterExecutablePath " +
            "and FileConversion:PdfDocxConverterScriptPath explicitly.";

        return $"{guidance} {exception.Message}";
    }

    private ProcessStartInfo CreateStartInfo(PdfDocxConverterProcessRequest request)
    {
        var inputPath = Path.GetFullPath(request.InputPath);
        var outputPath = Path.GetFullPath(request.OutputPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.PdfDocxConverterExecutablePath,
            WorkingDirectory = GetSafeWorkingDirectory(
                _options.PdfDocxConverterScriptPath ?? _options.PdfDocxConverterExecutablePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        CopySafeEnvironment(startInfo);

        if (!string.IsNullOrWhiteSpace(_options.PdfDocxConverterScriptPath))
        {
            startInfo.ArgumentList.Add(Path.GetFullPath(_options.PdfDocxConverterScriptPath));
        }

        startInfo.ArgumentList.Add("--input");
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputPath);
        AddNumericArgument(startInfo, "--max-input-bytes", _options.PdfDocxMaxInputBytes);
        AddNumericArgument(startInfo, "--max-output-bytes", _options.PdfDocxMaxOutputBytes);

        return startInfo;
    }

    private static async Task<bool> HasPdfHeaderAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[PdfHeaderSearchBytes];
        await using var stream = new FileStream(
            inputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            buffer.Length,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(bytesRead), cancellationToken);
            if (count == 0)
            {
                break;
            }

            bytesRead += count;
        }

        return buffer.AsSpan(0, bytesRead).IndexOf(PdfHeader) >= 0;
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
                logger.LogWarning(exception, "The PDF to DOCX conversion progress callback failed");
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

        // Keep the worker's newline-delimited JSON and diagnostics deterministic
        // when Python is launched with redirected streams on Windows.
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        startInfo.Environment["PYTHONUNBUFFERED"] = "1";
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
