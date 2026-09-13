using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPageAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cms_contents_kind_slug_is_template",
                table: "cms_contents");

            migrationBuilder.AddColumn<long>(
                name: "parent_id",
                table: "cms_contents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "parent_path",
                table: "cms_contents",
                type: "varchar(321)",
                maxLength: 321,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "previous_paths_json",
                table: "cms_contents",
                type: "json",
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "stored_path",
                table: "cms_contents",
                type: "varchar(482)",
                maxLength: 482,
                nullable: true,
                computedColumnSql: "concat_ws('/', `parent_path`, `slug`)",
                stored: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_cms_contents_kind_stored_path_is_template",
                table: "cms_contents",
                columns: new[] { "kind", "stored_path", "is_template" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cms_contents_parent_id",
                table: "cms_contents",
                column: "parent_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cms_contents_kind_stored_path_is_template",
                table: "cms_contents");

            migrationBuilder.DropIndex(
                name: "ix_cms_contents_parent_id",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "stored_path",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "parent_path",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "previous_paths_json",
                table: "cms_contents");

            migrationBuilder.CreateIndex(
                name: "ix_cms_contents_kind_slug_is_template",
                table: "cms_contents",
                columns: new[] { "kind", "slug", "is_template" },
                unique: true);
        }
    }
}
