using Utilities.Domain.Entities;

namespace Utilities.Application.Interfaces;

public interface IYoutubeJobRepository
{
    Task AddAsync(YoutubeJob job, CancellationToken cancellationToken);
    Task<YoutubeJob?> FindAsync(Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> FindInterruptedJobIdsAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
