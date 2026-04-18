using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kriteriom.BatchProcessor.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "batch_job_checkpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    last_processed_offset = table.Column<int>(type: "integer", nullable: false),
                    total_records = table.Column<int>(type: "integer", nullable: false),
                    processed_records = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_job_checkpoints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "batch_job_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_job_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_batch_job_checkpoints_job_name",
                table: "batch_job_checkpoints",
                column: "job_name");

            migrationBuilder.CreateIndex(
                name: "ix_batch_job_logs_job_name",
                table: "batch_job_logs",
                column: "job_name");

            migrationBuilder.CreateIndex(
                name: "ix_batch_job_logs_timestamp",
                table: "batch_job_logs",
                column: "timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "batch_job_checkpoints");

            migrationBuilder.DropTable(
                name: "batch_job_logs");
        }
    }
}
