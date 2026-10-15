using Microsoft.EntityFrameworkCore;
using Utilities.Application.Interfaces;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;
using Utilities.Infrastructure.Data.Context;

namespace Utilities.Infrastructure.Repositories;

public sealed class ConvertTaskRepository(AppDbContext dbContext) : IConvertTaskRepository
{
    public async Task AddAsync(ConvertTask task, CancellationToken cancellationToken)
    {
        dbContext.ConvertTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ConvertTask?> FindAsync(Guid jobId, CancellationToken cancellationToken) =>
        dbContext.ConvertTasks
            .Include(task => task.Job)
            .SingleOrDefaultAsync(task => task.JobId == jobId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> FindInterruptedJobIdsAsync(CancellationToken cancellationToken) =>
        await dbContext.ConvertTasks
            .Where(task => task.Status == JobStatuses.Pending || task.Status == JobStatuses.Running)
            .Select(task => task.JobId)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
