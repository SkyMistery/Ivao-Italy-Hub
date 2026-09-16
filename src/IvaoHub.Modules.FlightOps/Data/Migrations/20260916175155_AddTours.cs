using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.FlightOps.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fo_tours",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    slug = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_template = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    parent_tour_id = table.Column<long>(type: "bigint", nullable: true),
                    required_subtours = table.Column<int>(type: "int", nullable: true),
                    required_nm = table.Column<int>(type: "int", nullable: true),
                    open_goal = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    open_goal_json = table.Column<string>(type: "json", nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title_i18n = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    summary_i18n = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    briefing_json = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cover_media_id = table.Column<long>(type: "bigint", nullable: true),
                    banner_media_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    published_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    is_hidden = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    show_preview = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    release_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    close_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    report_window_days = table.Column<int>(type: "int", nullable: false),
                    progression = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    hub_rotation_order = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    requires_procedures = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    daily_leg_limit = table.Column<int>(type: "int", nullable: true),
                    allowed_aircraft_json = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    min_pilot_rating = table.Column<int>(type: "int", nullable: true),
                    reference_aircraft_icao = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: true, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    award_id = table.Column<long>(type: "bigint", nullable: true),
                    owner_department = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    owner_department_mask = table.Column<int>(type: "int", nullable: false),
                    visibility = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_by = table.Column<int>(type: "int", nullable: false),
                    row_version = table.Column<DateTime>(type: "timestamp(6)", rowVersion: true, nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fo_tours", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "ix_fo_tours_is_template_release_at",
                table: "fo_tours",
                columns: new[] { "is_template", "release_at" });

            migrationBuilder.CreateIndex(
                name: "ix_fo_tours_slug",
                table: "fo_tours",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fo_tours");
        }
    }
}
