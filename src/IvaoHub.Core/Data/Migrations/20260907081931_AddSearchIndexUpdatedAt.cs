using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchIndexUpdatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "cms_search_index",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // The rows that are already indexed have no date of their own, and the default of a
            // DateTime is the year 1 — which would sort every page an installation already had
            // *below* everything saved after this deploy, for ever, on every tie. They are stamped
            // with the moment of the migration instead: not when each was really written, which
            // nothing here knows, but the truthful "as far as this index is concerned, they are all
            // as old as each other". The next save of any of them writes the real thing.
            migrationBuilder.Sql("UPDATE cms_search_index SET updated_at = UTC_TIMESTAMP(6);");

            migrationBuilder.CreateIndex(
                name: "ix_cms_search_index_locale_updated_at",
                table: "cms_search_index",
                columns: new[] { "locale", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cms_search_index_locale_updated_at",
                table: "cms_search_index");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "cms_search_index");
        }
    }
}
