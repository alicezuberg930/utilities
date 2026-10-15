using Utilities.Domain.Constants;

namespace Utilities.Domain.Entities;

public sealed class YoutubeTask
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public string Status { get; set; } = JobStatuses.Pending;
    public required string Url { get; set; }
    public double? AudioQuality { get; set; }
    public string? VideoQuality { get; set; }
    public required string Format { get; set; }
}
