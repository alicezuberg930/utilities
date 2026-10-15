using Utilities.Domain.Entities;

namespace Utilities.Application.Interfaces;

public interface IConvertTaskRepository
{
    Task AddAsync(ConvertTask task, CancellationToken cancellationToken);
    Task<ConvertTask?> FindAsync(Guid jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> FindInterruptedJobIdsAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
