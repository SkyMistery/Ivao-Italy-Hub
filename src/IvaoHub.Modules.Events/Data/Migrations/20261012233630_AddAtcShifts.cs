using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.Events.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAtcShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evt_atc_shifts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    event_id = table.Column<long>(type: "bigint", nullable: false),
                    position_id = table.Column<long>(type: "bigint", nullable: false),
                    controller_vid = table.Column<int>(type: "int", nullable: false),
                    fir = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    from_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    to_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    origin = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reason = table.Column<string>(type: "json", nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notified_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    attendance = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    attendance_by = table.Column<int>(type: "int", nullable: true),
                    attendance_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    attendance_note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, collation: "utf8mb4_unicode_ci")
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
                    table.PrimaryKey("pk_evt_atc_shifts", x => x.id);
                    table.ForeignKey(
                        name: "fk_evt_atc_shifts_evt_atc_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "evt_atc_positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "ix_evt_atc_shifts_controller_vid",
                table: "evt_atc_shifts",
                column: "controller_vid");

            migrationBuilder.CreateIndex(
                name: "ix_evt_atc_shifts_event_id_from_utc",
                table: "evt_atc_shifts",
                columns: new[] { "event_id", "from_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_evt_atc_shifts_position_id",
                table: "evt_atc_shifts",
                column: "position_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evt_atc_shifts");
        }
    }
}
