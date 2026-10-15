using Utilities.Application.DTOs.Requests;
using Utilities.Application.DTOs.Responses;
using Utilities.Application.Exceptions;
using Utilities.Application.Interfaces;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;

namespace Utilities.Application.Services;

public sealed class YoutubeService(
    IYoutubeTaskRepository taskRepository,
    IYoutubeMetadataProvider metadataProvider,
    IYoutubeDownloadQueue downloadQueue,
    IYoutubeFileStorage fileStorage) : IYoutubeService
{
    public Task<YoutubeMetadataResponse> GetInfoAsync(string? url, CancellationToken cancellationToken)
    {
        ValidateUrl(url);
        return metadataProvider.GetInfoAsync(url!, cancellationToken);
    }

    public async Task<YoutubeJobResponse> CreateJobAsync(CreateYoutubeJobRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var now = DateTimeOffset.UtcNow;
        var job = new Job
        {
            JobId = Guid.CreateVersion7(),
            Progress = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var task = new YoutubeTask
        {
            JobId = job.JobId,
            Job = job,
            Status = JobStatuses.Pending,
            Url = request.Url,
            AudioQuality = request.AudioQuality,
            VideoQuality = request.VideoQuality,
            Format = request.Format,
        };

        await taskRepository.AddAsync(task, cancellationToken);
        await downloadQueue.QueueAsync(job.JobId, cancellationToken);

        return YoutubeJobResponse.FromEntity(task);
    }

    public async Task<JobState?> GetJobStateAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var task = await taskRepository.FindAsync(jobId, cancellationToken);
        return task is null
            ? null
            : new JobState(
                task.Job.Progress,
                task.Status == JobStatuses.Completed,
                task.Status == JobStatuses.Failed,
                task.Job.Error);
    }

    public async Task<JobDownloadFile> GetDownloadFileAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var task = await taskRepository.FindAsync(jobId, cancellationToken) ?? throw new ResourceNotFoundException("YouTube job was not found.");

        if (task.Status != JobStatuses.Completed)
        {
            throw new ResourceConflictException("The YouTube download is not complete.");
        }

        var path = fileStorage.GetOutputPath(task);
        if (!fileStorage.Exists(path))
        {
            throw new ResourceNotFoundException("The downloaded file was not found.");
        }

        var isAudio = task.Format == YoutubeFormats.Audio;
        return new JobDownloadFile(path, isAudio ? "audio/mpeg" : "video/mp4", $"{task.JobId:N}.{(isAudio ? "mp3" : "mp4")}");
    }

    private static void ValidateRequest(CreateYoutubeJobRequest request)
    {
        ValidateUrl(request.Url);

        if (!YoutubeFormats.All.Contains(request.Format))
        {
            throw new RequestValidationException("Format is invalid");
        }

        if (request.AudioQuality is not null && (!double.IsFinite(request.AudioQuality.Value) || request.AudioQuality <= 0))
        {
            throw new RequestValidationException("Audio quality is invalid");
        }

        if (request.VideoQuality is not null && !YoutubeQualities.Video.Contains(request.VideoQuality))
        {
            throw new RequestValidationException("Video quality is invalid");
        }

        if (request.Format == YoutubeFormats.Audio && request.AudioQuality is null)
        {
            throw new RequestValidationException("Audio format should only have audio quality");
        }

        if (request.Format == YoutubeFormats.Video && (request.VideoQuality is null || !YoutubeQualities.Video.Contains(request.VideoQuality)))
        {
            throw new RequestValidationException("Video format should only have video quality");
        }
    }

    private static void ValidateUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new RequestValidationException("Youtube URL cannot be empty");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new RequestValidationException("Youtube URL is invalid");
        }
    }
}
