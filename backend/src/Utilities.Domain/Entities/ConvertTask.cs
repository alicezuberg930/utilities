using Utilities.Domain.Constants;

namespace Utilities.Domain.Entities;

public sealed class ConvertTask
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public string Status { get; set; } = JobStatuses.Pending;
    public required string OriginalFormat { get; set; }
    public required string ConvertedFormat { get; set; }
    public required string DownloadUrl { get; set; }
}
