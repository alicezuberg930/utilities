using Utilities.Domain.Entities;

namespace Utilities.Application.Interfaces;

public interface IYoutubeTaskRepository
{
    Task AddAsync(YoutubeTask task, CancellationToken cancellationToken);
    Task<YoutubeTask?> FindAsync(Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> FindInterruptedJobIdsAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
