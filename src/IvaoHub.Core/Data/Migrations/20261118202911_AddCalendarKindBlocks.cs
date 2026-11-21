using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarKindBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "blocks",
                table: "cms_calendar_kinds",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "blocks",
                table: "cms_calendar_kinds");
        }
    }
}
