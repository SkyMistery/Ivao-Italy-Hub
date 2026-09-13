using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "vid",
                table: "hub_user_grants",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "position_department",
                table: "hub_user_grants",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "position_levels_json",
                table: "hub_user_grants",
                type: "json",
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_hub_user_grants_position_department",
                table: "hub_user_grants",
                column: "position_department");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_hub_user_grants_position_department",
                table: "hub_user_grants");

            migrationBuilder.DropColumn(
                name: "position_department",
                table: "hub_user_grants");

            migrationBuilder.DropColumn(
                name: "position_levels_json",
                table: "hub_user_grants");

            migrationBuilder.AlterColumn<int>(
                name: "vid",
                table: "hub_user_grants",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
