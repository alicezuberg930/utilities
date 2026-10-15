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
    public static YoutubeJobResponse FromEntity(YoutubeTask task) => new(
        task.JobId,
        task.Status,
        task.Url,
        task.AudioQuality,
        task.VideoQuality,
        task.Format,
        task.Job.Progress,
        task.Job.CreatedAt,
        task.Job.UpdatedAt);
}
