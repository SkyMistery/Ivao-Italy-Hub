using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.Events.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAfterTheEventColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "flown_unbooked_airports",
                table: "evt_event_stats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "covered_by_vid",
                table: "evt_atc_shifts",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "flown_unbooked_airports",
                table: "evt_event_stats");

            migrationBuilder.DropColumn(
                name: "covered_by_vid",
                table: "evt_atc_shifts");
        }
    }
}
