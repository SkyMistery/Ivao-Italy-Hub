using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.IntegrationTests.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleStakeholder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "stakeholder_vid",
                table: "smp_items",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "stakeholder_vid",
                table: "smp_items");
        }
    }
}
