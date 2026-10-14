using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Utilities.Domain.Entities;

namespace Utilities.Infrastructure.Data.Configurations;

public sealed class YoutubeJobConfiguration : IEntityTypeConfiguration<YoutubeJob>
{
    public void Configure(EntityTypeBuilder<YoutubeJob> builder)
    {
        builder.ToTable("youtube_jobs", table =>
        {
            table.HasCheckConstraint("ck_youtube_jobs_format", "format IN ('audio', 'video')");
            table.HasCheckConstraint(
                "ck_youtube_jobs_status",
                "status IN ('pending', 'running', 'completed', 'failed')");
            table.HasCheckConstraint(
                "ck_youtube_jobs_audio_quality",
                "audio_quality IS NULL OR audio_quality > 0");
            table.HasCheckConstraint(
                "ck_youtube_jobs_video_quality",
                "video_quality IS NULL OR video_quality IN ('360p', '480p', '720p', '1080p')");
            table.HasCheckConstraint("ck_youtube_jobs_progress", "progress >= 0 AND progress <= 100");
        });

        builder.HasKey(job => job.JobId);

        builder.Property(job => job.JobId).HasColumnName("job_id").ValueGeneratedNever();
        builder.Property(job => job.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        builder.Property(job => job.Url).HasColumnName("url").IsRequired();
        builder.Property(job => job.AudioQuality).HasColumnName("audio_quality");
        builder.Property(job => job.VideoQuality).HasColumnName("video_quality").HasMaxLength(16);
        builder.Property(job => job.Format).HasColumnName("format").HasMaxLength(8).IsRequired();
        builder.Property(job => job.Progress).HasColumnName("progress").HasDefaultValue(0d);
        builder.Property(job => job.Error).HasColumnName("error").HasMaxLength(4000);
        builder.Property(job => job.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(job => job.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(job => job.Status).HasDatabaseName("ix_youtube_jobs_status");
    }
}
