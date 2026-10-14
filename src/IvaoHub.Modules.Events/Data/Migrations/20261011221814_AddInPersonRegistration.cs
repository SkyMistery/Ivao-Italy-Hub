using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.Events.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInPersonRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evt_event_stats",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    public_slots = table.Column<int>(type: "int", nullable: false),
                    private_slots = table.Column<int>(type: "int", nullable: false),
                    booked = table.Column<int>(type: "int", nullable: false),
                    booked_flown = table.Column<int>(type: "int", nullable: false),
                    booked_unflown = table.Column<int>(type: "int", nullable: false),
                    flown_unbooked = table.Column<int>(type: "int", nullable: false),
                    atc_availabilities = table.Column<int>(type: "int", nullable: false),
                    atc_shifts = table.Column<int>(type: "int", nullable: false),
                    atc_attended = table.Column<int>(type: "int", nullable: false),
                    atc_no_shows = table.Column<int>(type: "int", nullable: false),
                    atc_excused = table.Column<int>(type: "int", nullable: false),
                    reports_submitted = table.Column<int>(type: "int", nullable: false),
                    reports_accepted = table.Column<int>(type: "int", nullable: false),
                    award_signals = table.Column<int>(type: "int", nullable: false),
                    registrations = table.Column<int>(type: "int", nullable: false),
                    answer_sums = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    activity_seats = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    computed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    owner_department = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    owner_department_mask = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evt_event_stats", x => x.id);
                    table.ForeignKey(
                        name: "fk_evt_event_stats_evt_events_event_id",
                        column: x => x.event_id,
                        principalTable: "evt_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "evt_questions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    ordinal = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    label_i18n = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    choices = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    required = table.Column<bool>(type: "tinyint(1)", nullable: false),
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
                    table.PrimaryKey("pk_evt_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_evt_questions_evt_events_event_id",
                        column: x => x.event_id,
                        principalTable: "evt_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "evt_registrations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    vid = table.Column<int>(type: "int", nullable: false),
                    answers = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    owner_department = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    owner_department_mask = table.Column<int>(type: "int", nullable: false),
                    visibility = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evt_registrations", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "ix_evt_event_stats_event_id",
                table: "evt_event_stats",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evt_questions_event_id",
                table: "evt_questions",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_evt_registrations_event_id_vid",
                table: "evt_registrations",
                columns: new[] { "event_id", "vid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evt_registrations_vid",
                table: "evt_registrations",
                column: "vid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evt_event_stats");

            migrationBuilder.DropTable(
                name: "evt_questions");

            migrationBuilder.DropTable(
                name: "evt_registrations");
        }
    }
}
