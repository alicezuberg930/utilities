using Utilities.Application.DTOs.Responses;

namespace Utilities.Application.Interfaces;

public interface IYoutubeMetadataProvider
{
    Task<YoutubeMetadataResponse> GetInfoAsync(string url, CancellationToken cancellationToken);
}
