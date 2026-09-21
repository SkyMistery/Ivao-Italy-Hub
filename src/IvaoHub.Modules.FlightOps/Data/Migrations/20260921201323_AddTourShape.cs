using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.FlightOps.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTourShape : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "close_from_parent",
                table: "fo_tours",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "release_from_parent",
                table: "fo_tours",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "fo_callsign_rules",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tour_id = table.Column<long>(type: "bigint", nullable: false),
                    leg_id = table.Column<long>(type: "bigint", nullable: true),
                    mode = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    match = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    value = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
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
                    table.PrimaryKey("pk_fo_callsign_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_fo_callsign_rules_fo_legs_leg_id",
                        column: x => x.leg_id,
                        principalTable: "fo_legs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fo_callsign_rules_fo_tours_tour_id",
                        column: x => x.tour_id,
                        principalTable: "fo_tours",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "fo_hubs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tour_id = table.Column<long>(type: "bigint", nullable: false),
                    icao = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sort = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("pk_fo_hubs", x => x.id);
                    table.ForeignKey(
                        name: "fk_fo_hubs_fo_tours_tour_id",
                        column: x => x.tour_id,
                        principalTable: "fo_tours",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "fo_rotations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tour_id = table.Column<long>(type: "bigint", nullable: false),
                    hub_id = table.Column<long>(type: "bigint", nullable: false),
                    sort = table.Column<int>(type: "int", nullable: false),
                    size = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("pk_fo_rotations", x => x.id);
                    table.ForeignKey(
                        name: "fk_fo_rotations_fo_hubs_hub_id",
                        column: x => x.hub_id,
                        principalTable: "fo_hubs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_fo_rotations_fo_tours_tour_id",
                        column: x => x.tour_id,
                        principalTable: "fo_tours",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "ix_fo_tours_parent_tour_id",
                table: "fo_tours",
                column: "parent_tour_id");

            migrationBuilder.CreateIndex(
                name: "ix_fo_legs_rotation_id",
                table: "fo_legs",
                column: "rotation_id");

            migrationBuilder.CreateIndex(
                name: "ix_fo_callsign_rules_leg_id",
                table: "fo_callsign_rules",
                column: "leg_id");

            migrationBuilder.CreateIndex(
                name: "ix_fo_callsign_rules_tour_id_leg_id",
                table: "fo_callsign_rules",
                columns: new[] { "tour_id", "leg_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fo_hubs_tour_id_icao",
                table: "fo_hubs",
                columns: new[] { "tour_id", "icao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fo_rotations_hub_id",
                table: "fo_rotations",
                column: "hub_id");

            migrationBuilder.CreateIndex(
                name: "ix_fo_rotations_tour_id_hub_id_sort",
                table: "fo_rotations",
                columns: new[] { "tour_id", "hub_id", "sort" });

            migrationBuilder.AddForeignKey(
                name: "fk_fo_legs_rotations_rotation_id",
                table: "fo_legs",
                column: "rotation_id",
                principalTable: "fo_rotations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_fo_tours_fo_tours_parent_tour_id",
                table: "fo_tours",
                column: "parent_tour_id",
                principalTable: "fo_tours",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_fo_legs_rotations_rotation_id",
                table: "fo_legs");

            migrationBuilder.DropForeignKey(
                name: "fk_fo_tours_fo_tours_parent_tour_id",
                table: "fo_tours");

            migrationBuilder.DropTable(
                name: "fo_callsign_rules");

            migrationBuilder.DropTable(
                name: "fo_rotations");

            migrationBuilder.DropTable(
                name: "fo_hubs");

            migrationBuilder.DropIndex(
                name: "ix_fo_tours_parent_tour_id",
                table: "fo_tours");

            migrationBuilder.DropIndex(
                name: "ix_fo_legs_rotation_id",
                table: "fo_legs");

            migrationBuilder.DropColumn(
                name: "close_from_parent",
                table: "fo_tours");

            migrationBuilder.DropColumn(
                name: "release_from_parent",
                table: "fo_tours");
        }
    }
}
