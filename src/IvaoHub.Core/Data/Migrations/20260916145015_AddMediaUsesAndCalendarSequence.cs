using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaUsesAndCalendarSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cms_calendar_entries_source_module_source_id",
                table: "cms_calendar_entries");

            migrationBuilder.AddColumn<int>(
                name: "sequence",
                table: "cms_calendar_entries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "cms_media_uses",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    source_module = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    media_id = table.Column<long>(type: "bigint", nullable: false),
                    used_until = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cms_media_uses", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "ix_cms_calendar_entries_source_module_source_id_sequence",
                table: "cms_calendar_entries",
                columns: new[] { "source_module", "source_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cms_media_uses_media_id_used_until",
                table: "cms_media_uses",
                columns: new[] { "media_id", "used_until" });

            migrationBuilder.CreateIndex(
                name: "ix_cms_media_uses_source_module_source_id_media_id",
                table: "cms_media_uses",
                columns: new[] { "source_module", "source_id", "media_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cms_media_uses");

            migrationBuilder.DropIndex(
                name: "ix_cms_calendar_entries_source_module_source_id_sequence",
                table: "cms_calendar_entries");

            migrationBuilder.DropColumn(
                name: "sequence",
                table: "cms_calendar_entries");

            migrationBuilder.CreateIndex(
                name: "ix_cms_calendar_entries_source_module_source_id",
                table: "cms_calendar_entries",
                columns: new[] { "source_module", "source_id" },
                unique: true);
        }
    }
}
