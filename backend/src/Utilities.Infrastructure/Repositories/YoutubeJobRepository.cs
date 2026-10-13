using Microsoft.EntityFrameworkCore;
using Utilities.Application.Interfaces;
using Utilities.Domain.Constants;
using Utilities.Domain.Entities;
using Utilities.Infrastructure.Data.Context;

namespace Utilities.Infrastructure.Repositories;

public sealed class YoutubeJobRepository(AppDbContext dbContext) : IYoutubeJobRepository
{
    public async Task AddAsync(YoutubeJob job, CancellationToken cancellationToken)
    {
        dbContext.YoutubeJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<YoutubeJob?> FindAsync(Guid jobId, CancellationToken cancellationToken) => dbContext.YoutubeJobs.SingleOrDefaultAsync(job => job.JobId == jobId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> FindInterruptedJobIdsAsync(CancellationToken cancellationToken) => await dbContext.YoutubeJobs
            .Where(job => job.Status == YoutubeJobStatuses.Pending || job.Status == YoutubeJobStatuses.Running)
            .Select(job => job.JobId)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
