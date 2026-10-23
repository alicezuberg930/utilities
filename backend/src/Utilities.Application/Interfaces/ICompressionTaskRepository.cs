using Utilities.Domain.Entities;

namespace Utilities.Application.Interfaces;

public interface ICompressionTaskRepository
{
    Task AddAsync(CompressionTask task, CancellationToken cancellationToken);
    Task<CompressionTask?> FindAsync(Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> FindInterruptedJobIdsAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
