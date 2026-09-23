using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.FlightOps.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDisputesAndLegIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "dispute_decided_at",
                table: "fo_pireps",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "dispute_decided_by_vid",
                table: "fo_pireps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dispute_status",
                table: "fo_pireps",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "dispute_text",
                table: "fo_pireps",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "disputed_at",
                table: "fo_pireps",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fo_leg_issues",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tour_id = table.Column<long>(type: "bigint", nullable: false),
                    leg_id = table.Column<long>(type: "bigint", nullable: false),
                    body = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    staff_note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true, collation: "utf8mb4_unicode_ci")
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
                    table.PrimaryKey("pk_fo_leg_issues", x => x.id);
                    table.ForeignKey(
                        name: "fk_fo_leg_issues_fo_legs_leg_id",
                        column: x => x.leg_id,
                        principalTable: "fo_legs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fo_leg_issues_fo_tours_tour_id",
                        column: x => x.tour_id,
                        principalTable: "fo_tours",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "ix_fo_leg_issues_leg_id",
                table: "fo_leg_issues",
                column: "leg_id");

            migrationBuilder.CreateIndex(
                name: "ix_fo_leg_issues_status_created_at",
                table: "fo_leg_issues",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_fo_leg_issues_tour_id",
                table: "fo_leg_issues",
                column: "tour_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fo_leg_issues");

            migrationBuilder.DropColumn(
                name: "dispute_decided_at",
                table: "fo_pireps");

            migrationBuilder.DropColumn(
                name: "dispute_decided_by_vid",
                table: "fo_pireps");

            migrationBuilder.DropColumn(
                name: "dispute_status",
                table: "fo_pireps");

            migrationBuilder.DropColumn(
                name: "dispute_text",
                table: "fo_pireps");

            migrationBuilder.DropColumn(
                name: "disputed_at",
                table: "fo_pireps");
        }
    }
}
