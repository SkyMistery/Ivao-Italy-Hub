using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.Events.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evt_bookings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    slot_id = table.Column<long>(type: "bigint", nullable: false),
                    booker_vid = table.Column<int>(type: "int", nullable: false),
                    aircraft_icao = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    callsign = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    other_icao = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    other_time_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    paired_booking_id = table.Column<long>(type: "bigint", nullable: true),
                    flown_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    flown_session_id = table.Column<long>(type: "bigint", nullable: true),
                    flown_checked_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    unflown_excused_by = table.Column<int>(type: "int", nullable: true),
                    unflown_excused_note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reminded_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    owner_department = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    owner_department_mask = table.Column<int>(type: "int", nullable: false),
                    visibility = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evt_bookings", x => x.id);
                    table.ForeignKey(
                        name: "fk_evt_bookings_evt_slots_slot_id",
                        column: x => x.slot_id,
                        principalTable: "evt_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "ix_evt_bookings_booker_vid",
                table: "evt_bookings",
                column: "booker_vid");

            migrationBuilder.CreateIndex(
                name: "ix_evt_bookings_event_id_booker_vid",
                table: "evt_bookings",
                columns: new[] { "event_id", "booker_vid" });

            migrationBuilder.CreateIndex(
                name: "ix_evt_bookings_slot_id",
                table: "evt_bookings",
                column: "slot_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evt_bookings");
        }
    }
}
