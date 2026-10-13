using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Utilities.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class InitialPostgres : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "youtube_jobs",
            columns: table => new
            {
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                url = table.Column<string>(type: "text", nullable: false),
                audio_quality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                video_quality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                format = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                progress = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_youtube_jobs", x => x.job_id);
                table.CheckConstraint("ck_youtube_jobs_audio_quality", "audio_quality IS NULL OR audio_quality IN ('128kbps', '196kbps', '320kbps')");
                table.CheckConstraint("ck_youtube_jobs_format", "format IN ('audio', 'video')");
                table.CheckConstraint("ck_youtube_jobs_progress", "progress >= 0 AND progress <= 100");
                table.CheckConstraint("ck_youtube_jobs_status", "status IN ('pending', 'running', 'completed', 'failed')");
                table.CheckConstraint("ck_youtube_jobs_video_quality", "video_quality IS NULL OR video_quality IN ('360p', '480p', '720p', '1080p')");
            });

        migrationBuilder.CreateIndex(
            name: "ix_youtube_jobs_status",
            table: "youtube_jobs",
            column: "status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "youtube_jobs");
    }
}
