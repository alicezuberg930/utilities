using Utilities.Domain.Entities;

namespace Utilities.Application.DTOs.Responses;

public sealed record ImageCompressionJobResponse(
    Guid JobId,
    string Status,
    int Quality,
    string OriginalFileName,
    string OriginalFormat,
    string OutputFormat,
    long OriginalSizeBytes,
    long? CompressedSizeBytes,
    string DownloadUrl,
    double Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ImageCompressionJobResponse FromEntity(CompressionTask task) => new(
        task.JobId,
        task.Status,
        task.Quality,
        task.OriginalFileName,
        task.OriginalFormat,
        task.OutputFormat,
        task.OriginalSizeBytes,
        task.CompressedSizeBytes,
        task.DownloadUrl,
        task.Job.Progress,
        task.Job.CreatedAt,
        task.Job.UpdatedAt);
}
