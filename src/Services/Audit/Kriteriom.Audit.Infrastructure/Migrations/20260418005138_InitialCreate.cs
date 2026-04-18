using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kriteriom.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "text", nullable: false),
                    occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    service_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_entity_id",
                table: "audit_records",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_event_id",
                table: "audit_records",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_event_type",
                table: "audit_records",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_occurred_on",
                table: "audit_records",
                column: "occurred_on");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_records");
        }
    }
}
