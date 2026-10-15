using Microsoft.Extensions.Options;
using Utilities.Application.Interfaces;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;
using Utilities.Infrastructure.Configuration;

namespace Utilities.Infrastructure.Services;

public sealed class YoutubeFileStorage(IOptions<YoutubeOptions> options) : IYoutubeFileStorage
{
    private readonly YoutubeOptions _options = options.Value;

    public string GetOutputPath(YoutubeTask task)
    {
        var extension = task.Format == YoutubeFormats.Audio ? "mp3" : "mp4";
        return Path.Combine(_options.JobsDirectory, $"{task.JobId:N}.{extension}");
    }

    public bool Exists(string path) => File.Exists(path);

    public void EnsureDirectoryExists() => Directory.CreateDirectory(_options.JobsDirectory);
}
