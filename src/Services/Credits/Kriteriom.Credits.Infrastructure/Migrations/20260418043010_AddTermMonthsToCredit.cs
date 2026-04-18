using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kriteriom.Credits.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTermMonthsToCredit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "term_months",
                table: "credits",
                type: "integer",
                nullable: false,
                defaultValue: 36);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "term_months",
                table: "credits");
        }
    }
}
