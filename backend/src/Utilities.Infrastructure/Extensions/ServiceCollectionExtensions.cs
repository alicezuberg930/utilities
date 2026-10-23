using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Utilities.Application.Interfaces;
using Utilities.Infrastructure.BackgroundServices;
using Utilities.Infrastructure.Configuration;
using Utilities.Infrastructure.Data.Context;
using Utilities.Infrastructure.External.Compression;
using Utilities.Infrastructure.External.Conversion;
using Utilities.Infrastructure.External.Youtube;
using Utilities.Infrastructure.Messaging;
using Utilities.Infrastructure.Realtime;
using Utilities.Infrastructure.Repositories;
using Utilities.Infrastructure.Services;

namespace Utilities.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var postgresConnection = configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgresConnection));
        services.AddSingleton<IValidateOptions<FileConversionOptions>, FileConversionOptionsValidator>();
        services
            .AddOptions<YoutubeOptions>()
            .Bind(configuration.GetSection(YoutubeOptions.SectionName))
            .PostConfigure(options =>
            {
                if (!Path.IsPathFullyQualified(options.ExecutablePath) &&
                    (options.ExecutablePath.Contains(Path.DirectorySeparatorChar) ||
                     options.ExecutablePath.Contains(Path.AltDirectorySeparatorChar)))
                {
                    options.ExecutablePath = Path.GetFullPath(
                        Path.Combine(environment.ContentRootPath, options.ExecutablePath));
                }
            });
        services
            .AddOptions<FileConversionOptions>()
            .Bind(configuration.GetSection(FileConversionOptions.SectionName))
            .PostConfigure(options =>
            {
                options.FfmpegExecutablePath = ResolveExecutablePath(
                    options.FfmpegExecutablePath,
                    environment.ContentRootPath);
                options.FfprobeExecutablePath = ResolveExecutablePath(
                    options.FfprobeExecutablePath,
                    environment.ContentRootPath);
                options.CadConverterExecutablePath = ResolveCadConverterExecutablePath(
                    options.CadConverterExecutablePath,
                    environment.ContentRootPath);
                ResolvePdfDocxConverter(options, environment.ContentRootPath);
                if (!Path.IsPathFullyQualified(options.JobsDirectory))
                {
                    options.JobsDirectory = Path.GetFullPath(
                        Path.Combine(environment.ContentRootPath, options.JobsDirectory));
                }
            })
            .ValidateOnStart();

        services.AddScoped<IYoutubeTaskRepository, YoutubeTaskRepository>();
        services.AddScoped<IConvertTaskRepository, ConvertTaskRepository>();
        services.AddScoped<ICompressionTaskRepository, CompressionTaskRepository>();
        services.AddScoped<IYoutubeMetadataProvider, YoutubeMetadataProvider>();
        services.AddSingleton<IYoutubeFileStorage, YoutubeFileStorage>();
        services.AddSingleton<IFileConversionStorage, FileConversionStorage>();
        services.AddSingleton<IImageCompressionStorage, ImageCompressionStorage>();

        services.AddSingleton<YoutubeDownloadQueue>();
        services.AddSingleton<IYoutubeDownloadQueue>(provider => provider.GetRequiredService<YoutubeDownloadQueue>());
        services.AddSingleton<FileConversionQueue>();
        services.AddSingleton<IFileConversionQueue>(provider => provider.GetRequiredService<FileConversionQueue>());
        services.AddSingleton<ImageCompressionQueue>();
        services.AddSingleton<IImageCompressionQueue>(provider =>
            provider.GetRequiredService<ImageCompressionQueue>());

        services.AddSingleton<JobProgressBroker>();
        services.AddSingleton<IYoutubeProgressStream>(provider => provider.GetRequiredService<JobProgressBroker>());
        services.AddSingleton<IFileProgressStream>(provider => provider.GetRequiredService<JobProgressBroker>());
        services.AddSingleton<IImageCompressionProgressStream>(provider =>
            provider.GetRequiredService<JobProgressBroker>());
        services.AddSingleton<IJobProgressPublisher>(provider => provider.GetRequiredService<JobProgressBroker>());

        services.AddSingleton<YoutubeDownloader>();
        services.AddSingleton<ICadConverterProcess, CadConverterProcess>();
        services.AddSingleton<IPdfDocxConverterProcess, PdfDocxConverterProcess>();
        services.AddSingleton<IFileConversionEngine, MediaFileConversionEngine>();
        services.AddSingleton<IFileConversionEngine, CadFileConversionEngine>();
        services.AddSingleton<IFileConversionEngine, PdfDocxFileConversionEngine>();
        services.AddSingleton<FileConversionDispatcher>();
        services.AddSingleton<IImageCompressionEngine, SkiaSharpCompressionEngine>();
        services.AddHostedService<YoutubeDownloadWorker>();
        services.AddHostedService<FileConversionWorker>();
        services.AddHostedService<ImageCompressionWorker>();

        return services;
    }

    private static string ResolveExecutablePath(string executablePath, string contentRootPath) =>
        !Path.IsPathFullyQualified(executablePath) &&
        (executablePath.Contains(Path.DirectorySeparatorChar) ||
         executablePath.Contains(Path.AltDirectorySeparatorChar))
            ? Path.GetFullPath(Path.Combine(contentRootPath, executablePath))
            : executablePath;

    private static string ResolveCadConverterExecutablePath(
        string executablePath,
        string contentRootPath)
    {
        var resolvedPath = ResolveExecutablePath(executablePath, contentRootPath);
        var defaultCommandName = OperatingSystem.IsWindows()
            ? "utilities-cad-converter.exe"
            : "utilities-cad-converter";
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!string.Equals(resolvedPath, defaultCommandName, comparison))
        {
            return resolvedPath;
        }

        var localWorkerPath = Path.GetFullPath(Path.Combine(
            contentRootPath,
            "..",
            "..",
            "vendor",
            "cad-converter-worker",
            "target",
            "release",
            defaultCommandName));

        return File.Exists(localWorkerPath) ? localWorkerPath : resolvedPath;
    }

    private static void ResolvePdfDocxConverter(
        FileConversionOptions options,
        string contentRootPath)
    {
        options.PdfDocxConverterExecutablePath = ResolveExecutablePath(
            options.PdfDocxConverterExecutablePath,
            contentRootPath);
        if (options.PdfDocxConverterScriptPath is not null &&
            !string.IsNullOrWhiteSpace(options.PdfDocxConverterScriptPath))
        {
            options.PdfDocxConverterScriptPath = Path.IsPathFullyQualified(
                options.PdfDocxConverterScriptPath)
                ? options.PdfDocxConverterScriptPath
                : Path.GetFullPath(Path.Combine(
                    contentRootPath,
                    options.PdfDocxConverterScriptPath));
        }

        var defaultCommandName = OperatingSystem.IsWindows()
            ? "utilities-pdf-docx-converter.exe"
            : "utilities-pdf-docx-converter";
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!string.Equals(
                options.PdfDocxConverterExecutablePath,
                defaultCommandName,
                comparison) ||
            options.PdfDocxConverterScriptPath is not null)
        {
            return;
        }

        var localWorkerRoot = Path.GetFullPath(Path.Combine(
            contentRootPath,
            "..",
            "..",
            "vendor",
            "pdf-docx-converter-worker"));
        var localWorkerScript = Path.Combine(localWorkerRoot, "worker.py");
        var localPython = OperatingSystem.IsWindows()
            ? Path.Combine(localWorkerRoot, ".venv", "Scripts", "python.exe")
            : Path.Combine(localWorkerRoot, ".venv", "bin", "python");

        if (File.Exists(localPython) && File.Exists(localWorkerScript))
        {
            options.PdfDocxConverterExecutablePath = localPython;
            options.PdfDocxConverterScriptPath = localWorkerScript;
        }
    }
}
