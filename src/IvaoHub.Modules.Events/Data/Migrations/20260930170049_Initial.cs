using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.Events.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "evt_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    slug = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    public_slots = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    private_slots = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    has_roster = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    whole_division = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    in_person = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    organizer = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    external_url = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title_i18n = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    summary_i18n = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    body_json = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    banner_media_id = table.Column<long>(type: "bigint", nullable: true),
                    visible_from_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    booking_opens_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    starts_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ends_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    shift_minutes = table.Column<int>(type: "int", nullable: true),
                    venue_i18n = table.Column<string>(type: "json", nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    published_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    visibility = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cancelled_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    cancelled_by = table.Column<int>(type: "int", nullable: true),
                    cancellation_note_i18n = table.Column<string>(type: "json", nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    roster_proposed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    after_done_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    restricted_window_hours = table.Column<int>(type: "int", nullable: true),
                    restricted_max_per_window = table.Column<int>(type: "int", nullable: true),
                    restricted_max_per_event = table.Column<int>(type: "int", nullable: true),
                    owner_department = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    owner_department_mask = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by = table.Column<int>(type: "int", nullable: false),
                    row_version = table.Column<DateTime>(type: "timestamp(6)", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evt_events", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "evt_event_airports",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    icao = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ordinal = table.Column<int>(type: "int", nullable: false),
                    max_movements_per_hour = table.Column<int>(type: "int", nullable: true),
                    max_arrivals_per_hour = table.Column<int>(type: "int", nullable: true),
                    max_departures_per_hour = table.Column<int>(type: "int", nullable: true),
                    owner_department = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    owner_department_mask = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by = table.Column<int>(type: "int", nullable: false),
                    row_version = table.Column<DateTime>(type: "timestamp(6)", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evt_event_airports", x => x.id);
                    table.ForeignKey(
                        name: "fk_evt_event_airports_evt_events_event_id",
                        column: x => x.event_id,
                        principalTable: "evt_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "ix_evt_event_airports_event_id_icao",
                table: "evt_event_airports",
                columns: new[] { "event_id", "icao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evt_events_slug",
                table: "evt_events",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evt_events_status_starts_at_utc",
                table: "evt_events",
                columns: new[] { "status", "starts_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evt_event_airports");

            migrationBuilder.DropTable(
                name: "evt_events");
        }
    }
}
