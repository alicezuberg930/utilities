using Utilities.Application.DTOs.Requests;
using Utilities.Application.DTOs.Responses;

namespace Utilities.Application.Interfaces;

public interface IFileConversion
{
    Task<FileConversionJobResponse> CreateJobAsync(
        CreateFileConversionRequest request,
        CancellationToken cancellationToken);

    Task<JobState?> GetJobStateAsync(Guid jobId, CancellationToken cancellationToken);
    Task<JobDownloadFile> GetDownloadFileAsync(Guid jobId, CancellationToken cancellationToken);
}
