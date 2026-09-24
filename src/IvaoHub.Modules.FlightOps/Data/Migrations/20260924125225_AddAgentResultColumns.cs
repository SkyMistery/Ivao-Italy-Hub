using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.FlightOps.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentResultColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "agent_version",
                table: "fo_check_results",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "by_vid",
                table: "fo_check_results",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "token_id",
                table: "fo_check_results",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "agent_version",
                table: "fo_check_results");

            migrationBuilder.DropColumn(
                name: "by_vid",
                table: "fo_check_results");

            migrationBuilder.DropColumn(
                name: "token_id",
                table: "fo_check_results");
        }
    }
}
