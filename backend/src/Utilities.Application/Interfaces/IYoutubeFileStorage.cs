using Utilities.Domain.Entities;

namespace Utilities.Application.Interfaces;

public interface IYoutubeFileStorage
{
    string GetOutputPath(YoutubeTask task);
    bool Exists(string path);
    void EnsureDirectoryExists();
}
