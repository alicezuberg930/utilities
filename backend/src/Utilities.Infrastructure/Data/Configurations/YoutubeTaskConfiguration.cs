using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Utilities.Domain.Entities;

namespace Utilities.Infrastructure.Data.Configurations;

public sealed class YoutubeTaskConfiguration : IEntityTypeConfiguration<YoutubeTask>
{
    public void Configure(EntityTypeBuilder<YoutubeTask> builder)
    {
        builder.ToTable("youtube_tasks", table =>
        {
            table.HasCheckConstraint("ck_youtube_tasks_format", "format IN ('audio', 'video')");
            table.HasCheckConstraint(
                "ck_youtube_tasks_status",
                "status IN ('pending', 'running', 'completed', 'failed')");
            table.HasCheckConstraint(
                "ck_youtube_tasks_audio_quality",
                "audio_quality IS NULL OR audio_quality > 0");
            table.HasCheckConstraint(
                "ck_youtube_tasks_video_quality",
                "video_quality IS NULL OR video_quality IN ('360p', '480p', '720p', '1080p')");
        });

        builder.HasKey(task => task.JobId);
        builder.Property(task => task.JobId).HasColumnName("job_id").ValueGeneratedNever();
        builder.Property(task => task.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        builder.Property(task => task.Url).HasColumnName("url").IsRequired();
        builder.Property(task => task.AudioQuality).HasColumnName("audio_quality");
        builder.Property(task => task.VideoQuality).HasColumnName("video_quality").HasMaxLength(16);
        builder.Property(task => task.Format).HasColumnName("format").HasMaxLength(8).IsRequired();

        builder.HasOne(task => task.Job)
            .WithOne(job => job.YoutubeTask)
            .HasForeignKey<YoutubeTask>(task => task.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(task => task.Status).HasDatabaseName("ix_youtube_tasks_status");
    }
}
