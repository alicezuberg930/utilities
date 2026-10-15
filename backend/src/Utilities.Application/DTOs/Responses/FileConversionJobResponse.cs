using Utilities.Domain.Entities;

namespace Utilities.Application.DTOs.Responses;

public sealed record FileConversionJobResponse(
    Guid JobId,
    string Status,
    string OriginalFormat,
    string ConvertedFormat,
    string DownloadUrl,
    double Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static FileConversionJobResponse FromEntity(ConvertTask task) => new(
        task.JobId,
        task.Status,
        task.OriginalFormat,
        task.ConvertedFormat,
        task.DownloadUrl,
        task.Job.Progress,
        task.Job.CreatedAt,
        task.Job.UpdatedAt);
}
