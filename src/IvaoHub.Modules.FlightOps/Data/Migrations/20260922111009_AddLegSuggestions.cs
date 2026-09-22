using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.FlightOps.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLegSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "callsigns_json",
                table: "fo_legs",
                type: "json",
                nullable: false,
                defaultValueSql: "'[]'",
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "flight_numbers_json",
                table: "fo_legs",
                type: "json",
                nullable: false,
                defaultValueSql: "'[]'",
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            // The one callsign and flight number a leg had become the first suggestion of each (T8, Carmine 22 September
            // 2026). The old columns stay and are no longer written: a later release drops them.
            migrationBuilder.Sql("UPDATE fo_legs SET callsigns_json = JSON_ARRAY(real_callsign) WHERE real_callsign IS NOT NULL AND real_callsign <> '';");
            migrationBuilder.Sql("UPDATE fo_legs SET flight_numbers_json = JSON_ARRAY(flight_number) WHERE flight_number IS NOT NULL AND flight_number <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "callsigns_json",
                table: "fo_legs");

            migrationBuilder.DropColumn(
                name: "flight_numbers_json",
                table: "fo_legs");
        }
    }
}
