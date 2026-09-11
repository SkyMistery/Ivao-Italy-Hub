using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document_type",
                table: "cms_contents",
                type: "varchar(8)",
                maxLength: 8,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "effective_on",
                table: "cms_contents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fir",
                table: "cms_contents",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "icao",
                table: "cms_contents",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "primary_position",
                table: "cms_contents",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "retired_at",
                table: "cms_contents",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "review_notified_at",
                table: "cms_contents",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "review_on",
                table: "cms_contents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "secondary_position",
                table: "cms_contents",
                type: "varchar(16)",
                maxLength: 16,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "show_footer",
                table: "cms_contents",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "superseded_by_id",
                table: "cms_contents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "airac",
                table: "cms_content_versions",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_cms_contents_kind_review_on",
                table: "cms_contents",
                columns: new[] { "kind", "review_on" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cms_contents_kind_review_on",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "document_type",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "effective_on",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "fir",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "icao",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "primary_position",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "retired_at",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "review_notified_at",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "review_on",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "secondary_position",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "show_footer",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "superseded_by_id",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "airac",
                table: "cms_content_versions");
        }
    }
}
