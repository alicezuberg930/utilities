using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Utilities.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitJobsIntoTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    progress = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.job_id);
                    table.CheckConstraint("ck_jobs_progress", "progress >= 0 AND progress <= 100");
                });

            migrationBuilder.CreateTable(
                name: "convert_tasks",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    original_format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    converted_format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    download_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_convert_tasks", x => x.job_id);
                    table.CheckConstraint("ck_convert_tasks_status", "status IN ('pending', 'running', 'completed', 'failed')");
                    table.ForeignKey(
                        name: "FK_convert_tasks_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "job_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "youtube_tasks",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    audio_quality = table.Column<double>(type: "double precision", nullable: true),
                    video_quality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    format = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_youtube_tasks", x => x.job_id);
                    table.CheckConstraint("ck_youtube_tasks_audio_quality", "audio_quality IS NULL OR audio_quality > 0");
                    table.CheckConstraint("ck_youtube_tasks_format", "format IN ('audio', 'video')");
                    table.CheckConstraint("ck_youtube_tasks_status", "status IN ('pending', 'running', 'completed', 'failed')");
                    table.CheckConstraint("ck_youtube_tasks_video_quality", "video_quality IS NULL OR video_quality IN ('360p', '480p', '720p', '1080p')");
                    table.ForeignKey(
                        name: "FK_youtube_tasks_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "job_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO jobs (job_id, progress, error, created_at, updated_at)
                SELECT job_id, progress, error, created_at, updated_at
                FROM youtube_jobs;

                INSERT INTO youtube_tasks (job_id, status, url, audio_quality, video_quality, format)
                SELECT job_id, status, url, audio_quality, video_quality, format
                FROM youtube_jobs;
                """);

            migrationBuilder.DropTable(
                name: "youtube_jobs");

            migrationBuilder.CreateIndex(
                name: "ix_convert_tasks_status",
                table: "convert_tasks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_youtube_tasks_status",
                table: "youtube_tasks",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "youtube_jobs",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    audio_quality = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    format = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    progress = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    video_quality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_youtube_jobs", x => x.job_id);
                    table.CheckConstraint("ck_youtube_jobs_audio_quality", "audio_quality IS NULL OR audio_quality > 0");
                    table.CheckConstraint("ck_youtube_jobs_format", "format IN ('audio', 'video')");
                    table.CheckConstraint("ck_youtube_jobs_progress", "progress >= 0 AND progress <= 100");
                    table.CheckConstraint("ck_youtube_jobs_status", "status IN ('pending', 'running', 'completed', 'failed')");
                    table.CheckConstraint("ck_youtube_jobs_video_quality", "video_quality IS NULL OR video_quality IN ('360p', '480p', '720p', '1080p')");
                });

            migrationBuilder.Sql(
                """
                INSERT INTO youtube_jobs (
                    job_id,
                    audio_quality,
                    created_at,
                    error,
                    format,
                    progress,
                    status,
                    updated_at,
                    url,
                    video_quality)
                SELECT
                    task.job_id,
                    task.audio_quality,
                    job.created_at,
                    job.error,
                    task.format,
                    job.progress,
                    task.status,
                    job.updated_at,
                    task.url,
                    task.video_quality
                FROM youtube_tasks AS task
                INNER JOIN jobs AS job ON job.job_id = task.job_id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_youtube_jobs_status",
                table: "youtube_jobs",
                column: "status");

            migrationBuilder.DropTable(
                name: "convert_tasks");

            migrationBuilder.DropTable(
                name: "youtube_tasks");

            migrationBuilder.DropTable(
                name: "jobs");
        }
    }
}
