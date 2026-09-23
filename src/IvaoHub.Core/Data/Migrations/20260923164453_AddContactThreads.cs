using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContactThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "cms_contact_messages",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "general",
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "participants_json",
                table: "cms_contact_messages",
                type: "json",
                nullable: false,
                defaultValueSql: "'[]'",
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "source_id",
                table: "cms_contact_messages",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "source_module",
                table: "cms_contact_messages",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                collation: "utf8mb4_unicode_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cms_contact_references",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    message_id = table.Column<long>(type: "bigint", nullable: false),
                    source_module = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    label_i18n = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cms_contact_references", x => x.id);
                    table.ForeignKey(
                        name: "fk_cms_contact_references_cms_contact_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "cms_contact_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateTable(
                name: "cms_contact_replies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    message_id = table.Column<long>(type: "bigint", nullable: false),
                    author_vid = table.Column<int>(type: "int", nullable: false),
                    side = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    body = table.Column<string>(type: "text", nullable: false, collation: "utf8mb4_unicode_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cms_contact_replies", x => x.id);
                    table.ForeignKey(
                        name: "fk_cms_contact_replies_cms_contact_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "cms_contact_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "ix_cms_contact_messages_created_by_created_at",
                table: "cms_contact_messages",
                columns: new[] { "created_by", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_cms_contact_messages_source_module_source_id_kind",
                table: "cms_contact_messages",
                columns: new[] { "source_module", "source_id", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cms_contact_references_message_id_source_module_source_id",
                table: "cms_contact_references",
                columns: new[] { "message_id", "source_module", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cms_contact_references_source_module_source_id",
                table: "cms_contact_references",
                columns: new[] { "source_module", "source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cms_contact_replies_message_id_created_at",
                table: "cms_contact_replies",
                columns: new[] { "message_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cms_contact_references");

            migrationBuilder.DropTable(
                name: "cms_contact_replies");

            migrationBuilder.DropIndex(
                name: "ix_cms_contact_messages_created_by_created_at",
                table: "cms_contact_messages");

            migrationBuilder.DropIndex(
                name: "ix_cms_contact_messages_source_module_source_id_kind",
                table: "cms_contact_messages");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "cms_contact_messages");

            migrationBuilder.DropColumn(
                name: "participants_json",
                table: "cms_contact_messages");

            migrationBuilder.DropColumn(
                name: "source_id",
                table: "cms_contact_messages");

            migrationBuilder.DropColumn(
                name: "source_module",
                table: "cms_contact_messages");
        }
    }
}
