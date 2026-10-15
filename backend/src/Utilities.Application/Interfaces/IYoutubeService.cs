using Utilities.Application.DTOs.Requests;
using Utilities.Application.DTOs.Responses;

namespace Utilities.Application.Interfaces;

public interface IYoutubeService
{
    Task<YoutubeMetadataResponse> GetInfoAsync(string? url, CancellationToken cancellationToken);
    Task<YoutubeJobResponse> CreateJobAsync(CreateYoutubeJobRequest request, CancellationToken cancellationToken);
    Task<JobState?> GetJobStateAsync(Guid jobId, CancellationToken cancellationToken);
    Task<JobDownloadFile> GetDownloadFileAsync(Guid jobId, CancellationToken cancellationToken);
}
