using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaSha256 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sha256",
                table: "cms_media",
                type: "char(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_cms_media_owner_department_sha256",
                table: "cms_media",
                columns: new[] { "owner_department", "sha256" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cms_media_owner_department_sha256",
                table: "cms_media");

            migrationBuilder.DropColumn(
                name: "sha256",
                table: "cms_media");
        }
    }
}
