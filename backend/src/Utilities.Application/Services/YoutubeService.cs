using Utilities.Application.DTOs.Requests;
using Utilities.Application.DTOs.Responses;
using Utilities.Application.Exceptions;
using Utilities.Application.Interfaces;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;

namespace Utilities.Application.Services;

public sealed class YoutubeService(
    IYoutubeJobRepository jobRepository,
    IYoutubeMetadataProvider metadataProvider,
    IYoutubeDownloadQueue downloadQueue,
    IYoutubeFileStorage fileStorage) : IYoutubeService
{
    public Task<YoutubeMetadataResponse> GetInfoAsync(string? url, CancellationToken cancellationToken)
    {
        ValidateUrl(url);
        return metadataProvider.GetInfoAsync(url!, cancellationToken);
    }

    public async Task<YoutubeJobResponse> CreateJobAsync(
        CreateYoutubeJobRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var now = DateTimeOffset.UtcNow;
        var job = new YoutubeJob
        {
            JobId = Guid.CreateVersion7(),
            Status = YoutubeJobStatuses.Pending,
            Url = request.Url,
            AudioQuality = request.AudioQuality,
            VideoQuality = request.VideoQuality,
            Format = request.Format,
            Progress = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await jobRepository.AddAsync(job, cancellationToken);
        await downloadQueue.QueueAsync(job.JobId, cancellationToken);

        return YoutubeJobResponse.FromEntity(job);
    }

    public async Task<YoutubeJobState?> GetJobStateAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await jobRepository.FindAsync(jobId, cancellationToken);
        return job is null
            ? null
            : new YoutubeJobState(
                job.Progress,
                job.Status == YoutubeJobStatuses.Completed,
                job.Status == YoutubeJobStatuses.Failed,
                job.Error);
    }

    public async Task<YoutubeDownloadFile> GetDownloadFileAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var job = await jobRepository.FindAsync(jobId, cancellationToken)
            ?? throw new ResourceNotFoundException("YouTube job was not found.");

        if (job.Status != YoutubeJobStatuses.Completed)
        {
            throw new ResourceConflictException("The YouTube download is not complete.");
        }

        var path = fileStorage.GetOutputPath(job);
        if (!fileStorage.Exists(path))
        {
            throw new ResourceNotFoundException("The downloaded file was not found.");
        }

        var isAudio = job.Format == YoutubeFormats.Audio;
        return new YoutubeDownloadFile(
            path,
            isAudio ? "audio/mpeg" : "video/mp4",
            $"{job.JobId:N}.{(isAudio ? "mp3" : "mp4")}");
    }

    private static void ValidateRequest(CreateYoutubeJobRequest request)
    {
        ValidateUrl(request.Url);

        if (!YoutubeFormats.All.Contains(request.Format))
        {
            throw new RequestValidationException("Format is invalid");
        }

        if (request.AudioQuality is not null && !YoutubeQualities.Audio.Contains(request.AudioQuality))
        {
            throw new RequestValidationException("Audio quality is invalid");
        }

        if (request.VideoQuality is not null && !YoutubeQualities.Video.Contains(request.VideoQuality))
        {
            throw new RequestValidationException("Video quality is invalid");
        }

        if (request.Format == YoutubeFormats.Audio &&
            (request.AudioQuality is null || !YoutubeQualities.Audio.Contains(request.AudioQuality)))
        {
            throw new RequestValidationException("Audio format should only have audio quality");
        }

        if (request.Format == YoutubeFormats.Video &&
            (request.VideoQuality is null || !YoutubeQualities.Video.Contains(request.VideoQuality)))
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

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new RequestValidationException("Youtube URL is invalid");
        }
    }
}
