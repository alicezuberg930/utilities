using Microsoft.EntityFrameworkCore;
using Utilities.Application.Interfaces;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;
using Utilities.Infrastructure.Data.Context;

namespace Utilities.Infrastructure.Repositories;

public sealed class CompressionTaskRepository(AppDbContext dbContext) : ICompressionTaskRepository
{
    public async Task AddAsync(CompressionTask task, CancellationToken cancellationToken)
    {
        dbContext.CompressionTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<CompressionTask?> FindAsync(Guid jobId, CancellationToken cancellationToken) =>
        dbContext.CompressionTasks
            .Include(task => task.Job)
            .SingleOrDefaultAsync(task => task.JobId == jobId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> FindInterruptedJobIdsAsync(
        CancellationToken cancellationToken) =>
        await dbContext.CompressionTasks
            .Where(task => task.Status == JobStatuses.Pending || task.Status == JobStatuses.Running)
            .Select(task => task.JobId)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
