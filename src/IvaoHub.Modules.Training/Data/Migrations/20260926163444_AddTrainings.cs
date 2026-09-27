using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.Training.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trn_bans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    vid = table.Column<int>(type: "int", nullable: false),
                    reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ends_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    lifted_by = table.Column<int>(type: "int", nullable: true),
                    lifted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("pk_trn_bans", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "trn_trainings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    kind = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rating = table.Column<int>(type: "int", nullable: false),
                    is_mock_exam = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    position = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    airport_icao = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fir = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    trainee_vid = table.Column<int>(type: "int", nullable: false),
                    trainee_rating_at_request = table.Column<int>(type: "int", nullable: true),
                    trainee_hours_at_request = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    theory_confirmed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    availability_text = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes_text = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    state = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rejection = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rejection_reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    decided_by = table.Column<int>(type: "int", nullable: true),
                    decided_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    trainer_vid = table.Column<int>(type: "int", nullable: true),
                    assigned_by = table.Column<int>(type: "int", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    scheduled_start_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    chosen_slot_id = table.Column<long>(type: "bigint", nullable: true),
                    reminded_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ready_for_mock_exam = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ready_for_exam = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    cooldown_waived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    general_comment = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    staff_comment = table.Column<string>(type: "text", nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    closed_by = table.Column<int>(type: "int", nullable: true),
                    closed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    close_reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    open_kind = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    owner_department = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    owner_department_mask = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by = table.Column<int>(type: "int", nullable: false),
                    row_version = table.Column<DateTime>(type: "timestamp(6)", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)"),
                    visibility = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trn_trainings", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "ix_trn_bans_vid",
                table: "trn_bans",
                column: "vid");

            migrationBuilder.CreateIndex(
                name: "ix_trn_trainings_state_scheduled_start_utc",
                table: "trn_trainings",
                columns: new[] { "state", "scheduled_start_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_trn_trainings_trainee_vid_open_kind",
                table: "trn_trainings",
                columns: new[] { "trainee_vid", "open_kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trn_trainings_trainer_vid_state",
                table: "trn_trainings",
                columns: new[] { "trainer_vid", "state" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trn_bans");

            migrationBuilder.DropTable(
                name: "trn_trainings");
        }
    }
}
