using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.IntegrationTests.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleAssignee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "assignee_vid",
                table: "smp_records",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "assignee_vid",
                table: "smp_records");
        }
    }
}
