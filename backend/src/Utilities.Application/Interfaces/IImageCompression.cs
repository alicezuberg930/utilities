using Utilities.Application.DTOs.Requests;
using Utilities.Application.DTOs.Responses;

namespace Utilities.Application.Interfaces;

public interface IImageCompression
{
    Task<ImageCompressionJobResponse> CreateJobAsync(
        CreateImageCompressionRequest request,
        CancellationToken cancellationToken);

    Task<JobState?> GetJobStateAsync(Guid jobId, CancellationToken cancellationToken);
    Task<JobDownloadFile> GetDownloadFileAsync(Guid jobId, CancellationToken cancellationToken);
}
