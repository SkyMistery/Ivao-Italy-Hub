using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.FlightOps.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "note_to_pilot",
                table: "fo_pireps",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "override_reason",
                table: "fo_pireps",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "queued_at",
                table: "fo_pireps",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "staff_note",
                table: "fo_pireps",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "threshold_overridden",
                table: "fo_pireps",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // The reports sent before the queue existed entered it when they were last sent.
            migrationBuilder.Sql("UPDATE fo_pireps SET queued_at = COALESCE(resubmitted_at, submitted_at);");

            migrationBuilder.CreateTable(
                name: "fo_pirep_errors",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    pirep_id = table.Column<long>(type: "bigint", nullable: false),
                    error_id = table.Column<long>(type: "bigint", nullable: false),
                    category = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    suggested_by_check = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    confirmed = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fo_pirep_errors", x => x.id);
                    table.ForeignKey(
                        name: "fk_fo_pirep_errors_fo_pireps_pirep_id",
                        column: x => x.pirep_id,
                        principalTable: "fo_pireps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "fo_pirep_tracks",
                columns: table => new
                {
                    pirep_flight_id = table.Column<long>(type: "bigint", nullable: false),
                    points_gzip = table.Column<byte[]>(type: "mediumblob", nullable: false),
                    point_count = table.Column<int>(type: "int", nullable: false),
                    stored_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fo_pirep_tracks", x => x.pirep_flight_id);
                    table.ForeignKey(
                        name: "fk_fo_pirep_tracks_pirep_flights_pirep_flight_id",
                        column: x => x.pirep_flight_id,
                        principalTable: "fo_pirep_flights",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "ix_fo_pireps_status_queued_at",
                table: "fo_pireps",
                columns: new[] { "status", "queued_at" });

            migrationBuilder.CreateIndex(
                name: "ix_fo_pirep_errors_error_id",
                table: "fo_pirep_errors",
                column: "error_id");

            migrationBuilder.CreateIndex(
                name: "ix_fo_pirep_errors_pirep_id_error_id",
                table: "fo_pirep_errors",
                columns: new[] { "pirep_id", "error_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fo_pirep_errors");

            migrationBuilder.DropTable(
                name: "fo_pirep_tracks");

            migrationBuilder.DropIndex(
                name: "ix_fo_pireps_status_queued_at",
                table: "fo_pireps");

            migrationBuilder.DropColumn(
                name: "note_to_pilot",
                table: "fo_pireps");

            migrationBuilder.DropColumn(
                name: "override_reason",
                table: "fo_pireps");

            migrationBuilder.DropColumn(
                name: "queued_at",
                table: "fo_pireps");

            migrationBuilder.DropColumn(
                name: "staff_note",
                table: "fo_pireps");

            migrationBuilder.DropColumn(
                name: "threshold_overridden",
                table: "fo_pireps");
        }
    }
}
