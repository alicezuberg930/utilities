using Microsoft.EntityFrameworkCore;
using Utilities.Domain.Entities;

namespace Utilities.Infrastructure.Data.Context;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<YoutubeTask> YoutubeTasks => Set<YoutubeTask>();
    public DbSet<ConvertTask> ConvertTasks => Set<ConvertTask>();
    public DbSet<CompressionTask> CompressionTasks => Set<CompressionTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
