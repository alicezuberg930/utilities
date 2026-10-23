using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Utilities.Domain.Entities;

namespace Utilities.Infrastructure.Data.Configurations;

public sealed class CompressionTaskConfiguration : IEntityTypeConfiguration<CompressionTask>
{
    public void Configure(EntityTypeBuilder<CompressionTask> builder)
    {
        builder.ToTable("compression_tasks", table =>
        {
            table.HasCheckConstraint(
                "ck_compression_tasks_status",
                "status IN ('pending', 'running', 'completed', 'failed')");
            table.HasCheckConstraint(
                "ck_compression_tasks_quality",
                "quality >= 1 AND quality <= 100");
            table.HasCheckConstraint(
                "ck_compression_tasks_original_size",
                "original_size_bytes > 0");
            table.HasCheckConstraint(
                "ck_compression_tasks_compressed_size",
                "compressed_size_bytes IS NULL OR compressed_size_bytes >= 0");
        });

        builder.HasKey(task => task.JobId);
        builder.Property(task => task.JobId).HasColumnName("job_id").ValueGeneratedNever();
        builder.Property(task => task.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        builder.Property(task => task.Quality).HasColumnName("quality").IsRequired();
        builder.Property(task => task.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255).IsRequired();
        builder.Property(task => task.OriginalFormat).HasColumnName("original_format").HasMaxLength(8).IsRequired();
        builder.Property(task => task.OutputFormat).HasColumnName("output_format").HasMaxLength(8).IsRequired();
        builder.Property(task => task.OriginalSizeBytes).HasColumnName("original_size_bytes").IsRequired();
        builder.Property(task => task.CompressedSizeBytes).HasColumnName("compressed_size_bytes");
        builder.Property(task => task.DownloadUrl).HasColumnName("download_url").HasMaxLength(512).IsRequired();

        builder.HasOne(task => task.Job)
            .WithOne(job => job.CompressionTask)
            .HasForeignKey<CompressionTask>(task => task.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(task => task.Status).HasDatabaseName("ix_compression_tasks_status");
    }
}
