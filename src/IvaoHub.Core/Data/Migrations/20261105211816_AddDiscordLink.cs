using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "discord_linked_at",
                table: "hub_users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discord_username",
                table: "hub_users",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_hub_users_discord_id",
                table: "hub_users",
                column: "discord_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_hub_users_discord_id",
                table: "hub_users");

            migrationBuilder.DropColumn(
                name: "discord_linked_at",
                table: "hub_users");

            migrationBuilder.DropColumn(
                name: "discord_username",
                table: "hub_users");
        }
    }
}
