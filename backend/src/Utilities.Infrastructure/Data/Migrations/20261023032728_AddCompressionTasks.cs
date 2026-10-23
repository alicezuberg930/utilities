using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Utilities.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompressionTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "compression_tasks",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quality = table.Column<int>(type: "integer", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    original_format = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    output_format = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    original_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    compressed_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    download_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compression_tasks", x => x.job_id);
                    table.CheckConstraint("ck_compression_tasks_compressed_size", "compressed_size_bytes IS NULL OR compressed_size_bytes >= 0");
                    table.CheckConstraint("ck_compression_tasks_original_size", "original_size_bytes > 0");
                    table.CheckConstraint("ck_compression_tasks_quality", "quality >= 1 AND quality <= 100");
                    table.CheckConstraint("ck_compression_tasks_status", "status IN ('pending', 'running', 'completed', 'failed')");
                    table.ForeignKey(
                        name: "FK_compression_tasks_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "job_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_compression_tasks_status",
                table: "compression_tasks",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compression_tasks");
        }
    }
}
