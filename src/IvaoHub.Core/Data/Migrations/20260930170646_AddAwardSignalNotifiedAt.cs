using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAwardSignalNotifiedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "notified_at",
                table: "cms_award_signals",
                type: "datetime(6)",
                nullable: true);

            // The signals already in the queue were there before anybody could be told, and whoever assigns reads them in the
            // queue already: they are marked told as of when they arrived, so the first run does not present them as new
            // (note 2026-09-30-la-mail-a-chi-assegna-gli-award §3). Only the signals that arrive from now on are told.
            migrationBuilder.Sql("UPDATE cms_award_signals SET notified_at = created_at;");

            migrationBuilder.CreateIndex(
                name: "ix_cms_award_signals_status_notified_at",
                table: "cms_award_signals",
                columns: new[] { "status", "notified_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cms_award_signals_status_notified_at",
                table: "cms_award_signals");

            migrationBuilder.DropColumn(
                name: "notified_at",
                table: "cms_award_signals");
        }
    }
}
