using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Utilities.Application.Interfaces;
using Utilities.Infrastructure.BackgroundServices;
using Utilities.Infrastructure.Configuration;
using Utilities.Infrastructure.Data.Context;
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
                if (!Path.IsPathFullyQualified(options.JobsDirectory))
                {
                    options.JobsDirectory = Path.GetFullPath(
                        Path.Combine(environment.ContentRootPath, options.JobsDirectory));
                }
            });

        services.AddScoped<IYoutubeTaskRepository, YoutubeTaskRepository>();
        services.AddScoped<IConvertTaskRepository, ConvertTaskRepository>();
        services.AddScoped<IYoutubeMetadataProvider, YoutubeMetadataProvider>();
        services.AddSingleton<IYoutubeFileStorage, YoutubeFileStorage>();
        services.AddSingleton<IFileConversionStorage, FileConversionStorage>();

        services.AddSingleton<YoutubeDownloadQueue>();
        services.AddSingleton<IYoutubeDownloadQueue>(provider => provider.GetRequiredService<YoutubeDownloadQueue>());
        services.AddSingleton<FileConversionQueue>();
        services.AddSingleton<IFileConversionQueue>(provider => provider.GetRequiredService<FileConversionQueue>());

        services.AddSingleton<JobProgressBroker>();
        services.AddSingleton<IYoutubeProgressStream>(provider => provider.GetRequiredService<JobProgressBroker>());
        services.AddSingleton<IFileProgressStream>(provider => provider.GetRequiredService<JobProgressBroker>());
        services.AddSingleton<IJobProgressPublisher>(provider => provider.GetRequiredService<JobProgressBroker>());

        services.AddSingleton<YoutubeDownloader>();
        services.AddSingleton<FileConverter>();
        services.AddHostedService<YoutubeDownloadWorker>();
        services.AddHostedService<FileConversionWorker>();

        return services;
    }

    private static string ResolveExecutablePath(string executablePath, string contentRootPath) =>
        !Path.IsPathFullyQualified(executablePath) &&
        (executablePath.Contains(Path.DirectorySeparatorChar) ||
         executablePath.Contains(Path.AltDirectorySeparatorChar))
            ? Path.GetFullPath(Path.Combine(contentRootPath, executablePath))
            : executablePath;
}
