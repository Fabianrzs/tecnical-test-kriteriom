using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kriteriom.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventIdToNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "event_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_event_id",
                table: "notifications",
                column: "event_id",
                unique: true,
                filter: "event_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_event_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "event_id",
                table: "notifications");
        }
    }
}
