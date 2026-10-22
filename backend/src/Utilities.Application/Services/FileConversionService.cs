using Utilities.Application.DTOs.Requests;
using Utilities.Application.DTOs.Responses;
using Utilities.Application.Exceptions;
using Utilities.Application.Interfaces;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;

namespace Utilities.Application.Services;

public sealed class FileConversionService(
    IConvertTaskRepository taskRepository,
    IFileConversionQueue conversionQueue,
    IFileConversionStorage fileStorage) : IFileConversion
{
    public async Task<FileConversionJobResponse> CreateJobAsync(
        CreateFileConversionRequest request,
        CancellationToken cancellationToken)
    {
        var originalFormat = NormalizeOriginalFormat(request.FileName);
        var convertedFormat = NormalizeConvertedFormat(request.ConvertedFormat);

        if (request.Length <= 0)
        {
            throw new RequestValidationException("The source file cannot be empty.");
        }

        if (!FileConversionFormats.IsSupportedConversion(originalFormat, convertedFormat))
        {
            throw new RequestValidationException(
                $"Conversion from '{originalFormat}' to '{convertedFormat}' is not supported.");
        }

        var now = DateTimeOffset.UtcNow;
        var job = new Job
        {
            JobId = Guid.CreateVersion7(),
            Progress = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var task = new ConvertTask
        {
            JobId = job.JobId,
            Job = job,
            Status = JobStatuses.Pending,
            OriginalFormat = originalFormat,
            ConvertedFormat = convertedFormat,
            DownloadUrl = $"/api/v1/convert/file/{job.JobId}",
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

        await conversionQueue.QueueAsync(job.JobId, cancellationToken);
        return FileConversionJobResponse.FromEntity(task);
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
            throw new ResourceNotFoundException("File conversion job was not found.");

        if (task.Status != JobStatuses.Completed)
        {
            throw new ResourceConflictException("The file conversion is not complete.");
        }

        var path = fileStorage.GetOutputPath(task);
        if (!fileStorage.Exists(path))
        {
            throw new ResourceNotFoundException("The converted file was not found.");
        }

        return new JobDownloadFile(
            path,
            GetContentType(task.ConvertedFormat),
            $"{task.JobId:N}.{task.ConvertedFormat}");
    }

    private static string NormalizeOriginalFormat(string fileName)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName)).TrimStart('.').ToLowerInvariant();
        if (extension.Length == 0 || !FileConversionFormats.IsSupportedInput(extension))
        {
            throw new RequestValidationException("The source file format is not supported.");
        }

        return extension;
    }

    private static string NormalizeConvertedFormat(string? convertedFormat)
    {
        var extension = convertedFormat?.Trim().TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !FileConversionFormats.IsSupportedOutput(extension))
        {
            throw new RequestValidationException("The converted file format is not supported.");
        }

        return extension;
    }

    private static string GetContentType(string format) => format switch
    {
        "aac" => "audio/aac",
        "flac" => "audio/flac",
        "m4a" => "audio/mp4",
        "mp3" => "audio/mpeg",
        "ogg" or "opus" => "audio/ogg",
        "wav" => "audio/wav",
        "avi" => "video/x-msvideo",
        "mkv" => "video/x-matroska",
        "mov" => "video/quicktime",
        "mp4" => "video/mp4",
        "webm" => "video/webm",
        "apng" => "image/apng",
        "avif" => "image/avif",
        "bmp" => "image/bmp",
        "dpx" => "image/dpx",
        "exr" => "image/x-exr",
        "fits" => "image/fits",
        "gif" => "image/gif",
        "hdr" => "image/vnd.radiance",
        "ico" => "image/vnd.microsoft.icon",
        "j2c" or "j2k" or "jls" or "jp2" => "image/jp2",
        "jpeg" or "jpg" or "ljpg" => "image/jpeg",
        "jxl" => "image/jxl",
        "pam" or "pnm" => "image/x-portable-anymap",
        "pbm" => "image/x-portable-bitmap",
        "pcx" => "image/x-pcx",
        "pfm" => "image/x-portable-floatmap",
        "pgm" or "pgmyuv" => "image/x-portable-graymap",
        "png" => "image/png",
        "ppm" => "image/x-portable-pixmap",
        "qoi" => "image/qoi",
        "ras" or "rs" or "sun" or "sunras" => "image/x-cmu-raster",
        "sgi" => "image/sgi",
        "tga" => "image/x-tga",
        "tif" or "tiff" => "image/tiff",
        "wbmp" => "image/vnd.wap.wbmp",
        "webp" => "image/webp",
        "xbm" => "image/x-xbitmap",
        "xpm" => "image/x-xpixmap",
        "glb" => "model/gltf-binary",
        "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream",
    };
}
