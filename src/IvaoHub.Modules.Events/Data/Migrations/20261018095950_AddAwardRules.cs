using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.Events.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAwardRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "auto_reports_cursor",
                table: "evt_events",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "evt_award_rules",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    award_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    criterion = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    departure_icao = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    arrival_icao = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    count = table.Column<int>(type: "int", nullable: true),
                    distance_nm = table.Column<int>(type: "int", nullable: true),
                    minutes = table.Column<int>(type: "int", nullable: true),
                    position_type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
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
                    table.PrimaryKey("pk_evt_award_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_evt_award_rules_evt_events_event_id",
                        column: x => x.event_id,
                        principalTable: "evt_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "evt_report_awards",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    report_id = table.Column<long>(type: "bigint", nullable: false),
                    rule_id = table.Column<long>(type: "bigint", nullable: false),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    vid = table.Column<int>(type: "int", nullable: false),
                    award_id = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    points_out = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    owner_department = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    owner_department_mask = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evt_report_awards", x => x.id);
                    table.ForeignKey(
                        name: "fk_evt_report_awards_evt_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "evt_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "ix_evt_award_rules_event_id",
                table: "evt_award_rules",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_evt_report_awards_event_id",
                table: "evt_report_awards",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_evt_report_awards_report_id_rule_id",
                table: "evt_report_awards",
                columns: new[] { "report_id", "rule_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evt_report_awards_vid",
                table: "evt_report_awards",
                column: "vid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evt_award_rules");

            migrationBuilder.DropTable(
                name: "evt_report_awards");

            migrationBuilder.DropColumn(
                name: "auto_reports_cursor",
                table: "evt_events");
        }
    }
}
