using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Utilities.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseDynamicAudioBitrate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_youtube_jobs_audio_quality",
                table: "youtube_jobs");

            migrationBuilder.Sql(
                """
                ALTER TABLE youtube_jobs
                ALTER COLUMN audio_quality TYPE double precision
                USING CASE
                    WHEN audio_quality IS NULL THEN NULL
                    ELSE regexp_replace(audio_quality, 'kbps$', '')::double precision
                END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_youtube_jobs_audio_quality",
                table: "youtube_jobs",
                sql: "audio_quality IS NULL OR audio_quality > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_youtube_jobs_audio_quality",
                table: "youtube_jobs");

            migrationBuilder.Sql(
                """
                ALTER TABLE youtube_jobs
                ALTER COLUMN audio_quality TYPE character varying(16)
                USING CASE
                    WHEN audio_quality IN (128, 196, 320) THEN audio_quality::text || 'kbps'
                    ELSE NULL
                END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_youtube_jobs_audio_quality",
                table: "youtube_jobs",
                sql: "audio_quality IS NULL OR audio_quality IN ('128kbps', '196kbps', '320kbps')");
        }
    }
}
