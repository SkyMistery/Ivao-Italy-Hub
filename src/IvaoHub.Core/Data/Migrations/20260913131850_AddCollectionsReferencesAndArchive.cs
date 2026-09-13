using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionsReferencesAndArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "archived_at",
                table: "cms_media",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "collections_json",
                table: "cms_contents",
                type: "json",
                nullable: false,
                defaultValue: "[]",
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            // The single category becomes the first collection of the row (G20). The column stays,
            // unread and unwritten, until a contract release drops it.
            migrationBuilder.Sql(
                "UPDATE `cms_contents` SET `collections_json` = JSON_ARRAY(`category`) "
                + "WHERE `category` IS NOT NULL AND `category` <> '';");

            migrationBuilder.CreateTable(
                name: "cms_content_references",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    content_id = table.Column<long>(type: "bigint", nullable: false),
                    version_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<int>(type: "int", nullable: false),
                    target = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cms_content_references", x => x.id);
                    table.ForeignKey(
                        name: "fk_cms_content_references_cms_contents_content_id",
                        column: x => x.content_id,
                        principalTable: "cms_contents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cms_content_references_content_versions_version_id",
                        column: x => x.version_id,
                        principalTable: "cms_content_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "ix_cms_content_references_content_id",
                table: "cms_content_references",
                column: "content_id");

            migrationBuilder.CreateIndex(
                name: "ix_cms_content_references_kind_target",
                table: "cms_content_references",
                columns: new[] { "kind", "target" });

            migrationBuilder.CreateIndex(
                name: "ix_cms_content_references_version_id",
                table: "cms_content_references",
                column: "version_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cms_content_references");

            migrationBuilder.DropColumn(
                name: "archived_at",
                table: "cms_media");

            migrationBuilder.DropColumn(
                name: "collections_json",
                table: "cms_contents");
        }
    }
}
