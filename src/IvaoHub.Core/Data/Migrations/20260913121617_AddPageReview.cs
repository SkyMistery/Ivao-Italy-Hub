using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPageReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "proposed_menu_json",
                table: "cms_contents",
                type: "json",
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ready_at",
                table: "cms_contents",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ready_by",
                table: "cms_contents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_note",
                table: "cms_contents",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "approved_by",
                table: "cms_content_versions",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "proposed_menu_json",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "ready_at",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "ready_by",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "review_note",
                table: "cms_contents");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "cms_content_versions");
        }
    }
}
