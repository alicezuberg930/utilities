using Utilities.Domain.Entities;

namespace Utilities.Application.Interfaces;

public interface IYoutubeFileStorage
{
    string GetOutputPath(YoutubeJob job);
    bool Exists(string path);
    void EnsureDirectoryExists();
}
