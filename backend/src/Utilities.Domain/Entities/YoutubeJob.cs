using Utilities.Domain.Constants;

namespace Utilities.Domain.Entities;

public sealed class YoutubeJob
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = YoutubeJobStatuses.Pending;
    public required string Url { get; set; }
    public double? AudioQuality { get; set; }
    public string? VideoQuality { get; set; }
    public required string Format { get; set; }
    public double Progress { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
