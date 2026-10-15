namespace Utilities.Domain.Entities;

public sealed class Job
{
    public Guid JobId { get; set; }
    public double Progress { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public YoutubeTask? YoutubeTask { get; set; }
    public ConvertTask? ConvertTask { get; set; }
}
