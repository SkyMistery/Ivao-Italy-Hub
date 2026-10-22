using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishedFieldsToVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "collections_json",
                table: "cms_content_versions",
                type: "json",
                nullable: false,
                defaultValue: "[]",
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "cover_media_id",
                table: "cms_content_versions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "file_media_id",
                table: "cms_content_versions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seo_i18n",
                table: "cms_content_versions",
                type: "json",
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "show_footer",
                table: "cms_content_versions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "summary_i18n",
                table: "cms_content_versions",
                type: "json",
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            // What the public read until this release came from the row, live: the version now online
            // of every row is given those values, so nothing a visitor sees changes with the upgrade
            // (note 2026-10-21-i-campi-fuori-dalla-versione). Only the version a row points at: an
            // older one is never read again. Run once, as a migration is; run again it would write the
            // same values.
            migrationBuilder.Sql(
                "UPDATE `cms_content_versions` AS `version` "
                + "JOIN `cms_contents` AS `content` ON `content`.`published_version_id` = `version`.`id` "
                + "SET `version`.`summary_i18n` = `content`.`summary_i18n`, "
                + "`version`.`seo_i18n` = `content`.`seo_i18n`, "
                + "`version`.`collections_json` = `content`.`collections_json`, "
                + "`version`.`cover_media_id` = `content`.`cover_media_id`, "
                + "`version`.`file_media_id` = `content`.`file_media_id`, "
                + "`version`.`show_footer` = `content`.`show_footer`;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "collections_json",
                table: "cms_content_versions");

            migrationBuilder.DropColumn(
                name: "cover_media_id",
                table: "cms_content_versions");

            migrationBuilder.DropColumn(
                name: "file_media_id",
                table: "cms_content_versions");

            migrationBuilder.DropColumn(
                name: "seo_i18n",
                table: "cms_content_versions");

            migrationBuilder.DropColumn(
                name: "show_footer",
                table: "cms_content_versions");

            migrationBuilder.DropColumn(
                name: "summary_i18n",
                table: "cms_content_versions");
        }
    }
}
