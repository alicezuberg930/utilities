using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Utilities.Application.Interfaces;
using Utilities.Domain.Constants;
using Utilities.Infrastructure.External.Youtube;

namespace Utilities.Infrastructure.BackgroundServices;

public sealed class YoutubeDownloadWorker(
    IServiceScopeFactory scopeFactory,
    IYoutubeDownloadQueue queue,
    IJobProgressPublisher progressPublisher,
    IYoutubeFileStorage fileStorage,
    YoutubeDownloader downloader,
    ILogger<YoutubeDownloadWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RequeueInterruptedJobsAsync(stoppingToken);

            await foreach (var jobId in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await DownloadAsync(jobId, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Unhandled failure while processing YouTube job {JobId}", jobId);
                    await MarkFailedAsync(jobId, "The download failed unexpectedly.", CancellationToken.None);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }

    private async Task RequeueInterruptedJobsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IYoutubeTaskRepository>();
        var jobIds = await repository.FindInterruptedJobIdsAsync(cancellationToken);

        foreach (var jobId in jobIds)
        {
            await queue.QueueAsync(jobId, cancellationToken);
        }
    }

    private async Task DownloadAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IYoutubeTaskRepository>();
        var task = await repository.FindAsync(jobId, cancellationToken);

        if (task is null || task.Status is not (JobStatuses.Pending or JobStatuses.Running))
        {
            return;
        }

        task.Status = JobStatuses.Running;
        task.Job.Error = null;
        task.Job.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);

        fileStorage.EnsureDirectoryExists();
        var outputPath = fileStorage.GetOutputPath(task);
        var result = await downloader.DownloadAsync(
            task,
            outputPath,
            progress => progressPublisher.PublishProgress(jobId, progress),
            cancellationToken);

        if (!result.Success)
        {
            task.Status = JobStatuses.Failed;
            task.Job.Error = result.Error;
            task.Job.UpdatedAt = DateTimeOffset.UtcNow;
            await repository.SaveChangesAsync(CancellationToken.None);
            progressPublisher.Fail(jobId, result.Error ?? "The download failed.");
            return;
        }

        task.Status = JobStatuses.Completed;
        task.Job.Progress = 100;
        task.Job.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);

        progressPublisher.PublishProgress(jobId, 100);
        progressPublisher.Complete(jobId);
    }

    private async Task MarkFailedAsync(Guid jobId, string message, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IYoutubeTaskRepository>();
        var task = await repository.FindAsync(jobId, cancellationToken);

        if (task is not null)
        {
            task.Status = JobStatuses.Failed;
            task.Job.Error = message;
            task.Job.UpdatedAt = DateTimeOffset.UtcNow;
            await repository.SaveChangesAsync(cancellationToken);
        }

        progressPublisher.Fail(jobId, message);
    }
}
