using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.Training.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBanChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "follows_ban_id",
                table: "trn_bans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // The bans already written, chained in the order they were written (issue #434): each follows the one before it of the same
            // member, the first follows none. Within a member every row has its own predecessor, so the unique index below cannot be
            // refused by them. MariaDB updates a table from a derived table of its own (10.3.2+); its LAG takes no default, hence COALESCE.
            migrationBuilder.Sql(
                "UPDATE trn_bans AS ban JOIN (SELECT id, COALESCE(LAG(id) OVER (PARTITION BY vid ORDER BY id), 0) AS follows FROM trn_bans) AS chain ON chain.id = ban.id SET ban.follows_ban_id = chain.follows;");

            migrationBuilder.CreateIndex(
                name: "ix_trn_bans_vid_follows_ban_id",
                table: "trn_bans",
                columns: new[] { "vid", "follows_ban_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_trn_bans_vid_follows_ban_id",
                table: "trn_bans");

            migrationBuilder.DropColumn(
                name: "follows_ban_id",
                table: "trn_bans");
        }
    }
}
