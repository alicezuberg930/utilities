using Utilities.Application.DTOs.Requests;
using Utilities.Application.DTOs.Responses;
using Utilities.Application.Exceptions;
using Utilities.Application.Interfaces;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;

namespace Utilities.Application.Services;

public sealed class ImageCompressionService(
    ICompressionTaskRepository taskRepository,
    IImageCompressionQueue compressionQueue,
    IImageCompressionStorage fileStorage) : IImageCompression
{
    public async Task<ImageCompressionJobResponse> CreateJobAsync(
        CreateImageCompressionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Length <= 0)
        {
            throw new RequestValidationException("The source image cannot be empty.");
        }

        if (request.Quality is < 1 or > 100)
        {
            throw new RequestValidationException("Image quality must be between 1 and 100.");
        }

        var originalFileName = Path.GetFileName(request.FileName);
        if (string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Length > 255)
        {
            throw new RequestValidationException("The source image file name is invalid.");
        }

        var originalFormat = NormalizeOriginalFormat(originalFileName);
        var outputFormat = ImageCompressionFormats.GetOutputFormat(
            originalFormat,
            request.ConvertToWebp);
        var now = DateTimeOffset.UtcNow;
        var job = new Job
        {
            JobId = Guid.CreateVersion7(),
            Progress = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var task = new CompressionTask
        {
            JobId = job.JobId,
            Job = job,
            Quality = request.Quality,
            OriginalFileName = originalFileName,
            OriginalFormat = originalFormat,
            OutputFormat = outputFormat,
            OriginalSizeBytes = request.Length,
            DownloadUrl = $"/api/v1/image-compression/file/{job.JobId}",
        };

        fileStorage.EnsureDirectoryExists();
        var inputPath = fileStorage.GetInputPath(job.JobId, originalFormat);
        try
        {
            await fileStorage.SaveInputAsync(
                job.JobId,
                originalFormat,
                request.Content,
                cancellationToken);
        }
        catch
        {
            fileStorage.DeleteIfExists(inputPath);
            throw;
        }

        try
        {
            await taskRepository.AddAsync(task, cancellationToken);
        }
        catch
        {
            fileStorage.DeleteIfExists(inputPath);
            throw;
        }

        await compressionQueue.QueueAsync(job.JobId, cancellationToken);
        return ImageCompressionJobResponse.FromEntity(task);
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

    public async Task<JobDownloadFile> GetDownloadFileAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var task = await taskRepository.FindAsync(jobId, cancellationToken) ??
            throw new ResourceNotFoundException("Image compression job was not found.");

        if (task.Status != JobStatuses.Completed)
        {
            throw new ResourceConflictException("The image compression is not complete.");
        }

        var path = fileStorage.GetOutputPath(task);
        if (!fileStorage.Exists(path))
        {
            throw new ResourceNotFoundException("The compressed image was not found.");
        }

        var baseName = Path.GetFileNameWithoutExtension(task.OriginalFileName);
        var contentType = task.OutputFormat switch
        {
            "jpg" => "image/jpeg",
            "png" => "image/png",
            _ => "image/webp",
        };
        return new JobDownloadFile(
            path,
            contentType,
            $"{baseName}-compressed.{task.OutputFormat}");
    }

    private static string NormalizeOriginalFormat(string fileName)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (!ImageCompressionFormats.IsSupportedInput(extension))
        {
            throw new RequestValidationException(
                "Only JPEG, PNG, and WebP images can be compressed.");
        }

        return extension;
    }
}
