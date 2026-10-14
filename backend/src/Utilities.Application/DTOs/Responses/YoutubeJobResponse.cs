using Utilities.Domain.Entities;

namespace Utilities.Application.DTOs.Responses;

public sealed record YoutubeJobResponse(
    Guid JobId,
    string Status,
    string Url,
    double? AudioQuality,
    string? VideoQuality,
    string Format,
    double Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static YoutubeJobResponse FromEntity(YoutubeJob job) => new(
        job.JobId,
        job.Status,
        job.Url,
        job.AudioQuality,
        job.VideoQuality,
        job.Format,
        job.Progress,
        job.CreatedAt,
        job.UpdatedAt);
}
