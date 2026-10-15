using Microsoft.EntityFrameworkCore;
using Utilities.Application.Interfaces;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;
using Utilities.Infrastructure.Data.Context;

namespace Utilities.Infrastructure.Repositories;

public sealed class YoutubeTaskRepository(AppDbContext dbContext) : IYoutubeTaskRepository
{
    public async Task AddAsync(YoutubeTask task, CancellationToken cancellationToken)
    {
        dbContext.YoutubeTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<YoutubeTask?> FindAsync(Guid jobId, CancellationToken cancellationToken) =>
        dbContext.YoutubeTasks
            .Include(task => task.Job)
            .SingleOrDefaultAsync(task => task.JobId == jobId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> FindInterruptedJobIdsAsync(CancellationToken cancellationToken) => await dbContext.YoutubeTasks
            .Where(task => task.Status == JobStatuses.Pending || task.Status == JobStatuses.Running)
            .Select(task => task.JobId)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
