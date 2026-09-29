using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.IntegrationTests.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleFir : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fir",
                table: "smp_records",
                type: "varchar(8)",
                maxLength: 8,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fir",
                table: "smp_records");
        }
    }
}
