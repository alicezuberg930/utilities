using Utilities.Application.DTOs.Requests;
using Utilities.Application.DTOs.Responses;

namespace Utilities.Application.Interfaces;

public interface IYoutubeService
{
    Task<YoutubeMetadataResponse> GetInfoAsync(string? url, CancellationToken cancellationToken);
    Task<YoutubeJobResponse> CreateJobAsync(CreateYoutubeJobRequest request, CancellationToken cancellationToken);
    Task<YoutubeJobState?> GetJobStateAsync(Guid jobId, CancellationToken cancellationToken);
    Task<YoutubeDownloadFile> GetDownloadFileAsync(Guid jobId, CancellationToken cancellationToken);
}
