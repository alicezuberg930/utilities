using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Utilities.Domain.Entities;

namespace Utilities.Infrastructure.Data.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs", table =>
        {
            table.HasCheckConstraint("ck_jobs_progress", "progress >= 0 AND progress <= 100");
        });

        builder.HasKey(job => job.JobId);
        builder.Property(job => job.JobId).HasColumnName("job_id").ValueGeneratedNever();
        builder.Property(job => job.Progress).HasColumnName("progress").HasDefaultValue(0d);
        builder.Property(job => job.Error).HasColumnName("error").HasMaxLength(4000);
        builder.Property(job => job.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(job => job.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
