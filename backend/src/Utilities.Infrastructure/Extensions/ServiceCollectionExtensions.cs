using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Utilities.Application.Interfaces;
using Utilities.Infrastructure.BackgroundServices;
using Utilities.Infrastructure.Configuration;
using Utilities.Infrastructure.Data.Context;
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

        services.AddScoped<IYoutubeJobRepository, YoutubeJobRepository>();
        services.AddScoped<IYoutubeMetadataProvider, YoutubeMetadataProvider>();
        services.AddSingleton<IYoutubeFileStorage, YoutubeFileStorage>();

        services.AddSingleton<YoutubeDownloadQueue>();
        services.AddSingleton<IYoutubeDownloadQueue>(provider => provider.GetRequiredService<YoutubeDownloadQueue>());

        services.AddSingleton<YoutubeProgressBroker>();
        services.AddSingleton<IYoutubeProgressStream>(provider => provider.GetRequiredService<YoutubeProgressBroker>());
        services.AddSingleton<IYoutubeProgressPublisher>(provider => provider.GetRequiredService<YoutubeProgressBroker>());

        services.AddSingleton<YoutubeDownloader>();
        services.AddHostedService<YoutubeDownloadWorker>();

        return services;
    }
}
