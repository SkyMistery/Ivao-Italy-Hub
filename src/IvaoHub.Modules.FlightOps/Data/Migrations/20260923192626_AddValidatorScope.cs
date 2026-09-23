using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Modules.FlightOps.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddValidatorScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "scope_tour_id",
                table: "fo_pireps",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // The reports already sent take the tour a validator is enabled on (T15): their own, or their container's.
            migrationBuilder.Sql(
                "UPDATE fo_pireps p JOIN fo_tours t ON t.id = p.tour_id SET p.scope_tour_id = COALESCE(t.parent_tour_id, t.id);");

            migrationBuilder.CreateIndex(
                name: "ix_fo_pireps_decided_by_vid_decided_at",
                table: "fo_pireps",
                columns: new[] { "decided_by_vid", "decided_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fo_pireps_decided_by_vid_decided_at",
                table: "fo_pireps");

            migrationBuilder.DropColumn(
                name: "scope_tour_id",
                table: "fo_pireps");
        }
    }
}
