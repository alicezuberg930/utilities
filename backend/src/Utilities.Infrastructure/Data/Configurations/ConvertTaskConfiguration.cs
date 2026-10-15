using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Utilities.Domain.Entities;

namespace Utilities.Infrastructure.Data.Configurations;

public sealed class ConvertTaskConfiguration : IEntityTypeConfiguration<ConvertTask>
{
    public void Configure(EntityTypeBuilder<ConvertTask> builder)
    {
        builder.ToTable("convert_tasks", table =>
        {
            table.HasCheckConstraint(
                "ck_convert_tasks_status",
                "status IN ('pending', 'running', 'completed', 'failed')");
        });

        builder.HasKey(task => task.JobId);
        builder.Property(task => task.JobId).HasColumnName("job_id").ValueGeneratedNever();
        builder.Property(task => task.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        builder.Property(task => task.OriginalFormat).HasColumnName("original_format").HasMaxLength(16).IsRequired();
        builder.Property(task => task.ConvertedFormat).HasColumnName("converted_format").HasMaxLength(16).IsRequired();
        builder.Property(task => task.DownloadUrl).HasColumnName("download_url").HasMaxLength(512).IsRequired();

        builder.HasOne(task => task.Job)
            .WithOne(job => job.ConvertTask)
            .HasForeignKey<ConvertTask>(task => task.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(task => task.Status).HasDatabaseName("ix_convert_tasks_status");
    }
}
