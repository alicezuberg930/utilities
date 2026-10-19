using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Utilities.Application.Interfaces;
using Utilities.Domain.Constants;
using Utilities.Infrastructure.External.Conversion;

namespace Utilities.Infrastructure.BackgroundServices;

public sealed class FileConversionWorker(
    IServiceScopeFactory scopeFactory,
    IFileConversionQueue queue,
    IJobProgressPublisher progressPublisher,
    IFileConversionStorage fileStorage,
    FileConversionDispatcher converter,
    ILogger<FileConversionWorker> logger) : BackgroundService
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
                    await ConvertAsync(jobId, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Unhandled failure while processing conversion job {JobId}", jobId);
                    await MarkFailedAsync(jobId, "The file conversion failed unexpectedly.", CancellationToken.None);
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
        var repository = scope.ServiceProvider.GetRequiredService<IConvertTaskRepository>();
        var jobIds = await repository.FindInterruptedJobIdsAsync(cancellationToken);

        foreach (var jobId in jobIds)
        {
            await queue.QueueAsync(jobId, cancellationToken);
        }
    }

    private async Task ConvertAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IConvertTaskRepository>();
        var task = await repository.FindAsync(jobId, cancellationToken);

        if (task is null || task.Status is not (JobStatuses.Pending or JobStatuses.Running))
        {
            return;
        }

        task.Status = JobStatuses.Running;
        task.Job.Progress = 0;
        task.Job.Error = null;
        task.Job.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);

        var inputPath = fileStorage.GetInputPath(jobId, task.OriginalFormat);
        var outputPath = fileStorage.GetOutputPath(task);
        if (!fileStorage.Exists(inputPath))
        {
            await FailAsync(task, repository, "The source file was not found.", cancellationToken);
            return;
        }

        var lastProgress = 0d;
        var result = await converter.ConvertAsync(
            task,
            inputPath,
            outputPath,
            progress =>
            {
                lastProgress = progress;
                progressPublisher.PublishProgress(jobId, progress);
            },
            cancellationToken);

        fileStorage.DeleteIfExists(inputPath);

        if (!result.Success)
        {
            fileStorage.DeleteIfExists(outputPath);
            task.Job.Progress = lastProgress;
            await FailAsync(
                task,
                repository,
                result.Error ?? "The file conversion failed.",
                CancellationToken.None);
            return;
        }

        task.Status = JobStatuses.Completed;
        task.Job.Progress = 100;
        task.Job.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);

        progressPublisher.PublishProgress(jobId, 100);
        progressPublisher.Complete(jobId);
    }

    private async Task MarkFailedAsync(
        Guid jobId,
        string message,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IConvertTaskRepository>();
        var task = await repository.FindAsync(jobId, cancellationToken);

        if (task is not null)
        {
            await FailAsync(task, repository, message, cancellationToken);
            return;
        }

        progressPublisher.Fail(jobId, message);
    }

    private async Task FailAsync(
        Utilities.Domain.Entities.ConvertTask task,
        IConvertTaskRepository repository,
        string message,
        CancellationToken cancellationToken)
    {
        task.Status = JobStatuses.Failed;
        task.Job.Error = message;
        task.Job.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        progressPublisher.Fail(task.JobId, message);
    }
}
