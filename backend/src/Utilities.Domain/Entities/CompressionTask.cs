using Utilities.Domain.Constants;

namespace Utilities.Domain.Entities;

public sealed class CompressionTask
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public string Status { get; set; } = JobStatuses.Pending;
    public int Quality { get; set; }
    public required string OriginalFileName { get; set; }
    public required string OriginalFormat { get; set; }
    public required string OutputFormat { get; set; }
    public long OriginalSizeBytes { get; set; }
    public long? CompressedSizeBytes { get; set; }
    public required string DownloadUrl { get; set; }
}
