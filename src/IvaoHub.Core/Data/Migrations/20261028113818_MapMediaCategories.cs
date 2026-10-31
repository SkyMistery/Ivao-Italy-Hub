using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IvaoHub.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class MapMediaCategories : Migration
    {
        /// <summary>The mapping, public so that a test can run it on rows of its own inside a transaction.</summary>
        public const string MapSql =
            """
            UPDATE cms_media SET category = CASE
                WHEN content_type NOT LIKE 'image/%' THEN 'Document'
                WHEN category LIKE '%logo%' OR category LIKE '%loghi%' THEN 'Logo'
                WHEN category LIKE '%icon%' THEN 'Icon'
                WHEN category LIKE '%banner%' THEN 'Banner'
                ELSE 'Image'
            END;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The category of a file stops being free text (issue #359, note 2026-10-28-le-categorie-dei-media): what staff
            // wrote is carried onto the closed set of `MediaCategory`, and a row nobody had described gets what its bytes are.
            // No column changes, so the package before this one still reads every value written here as the text it is.
            // The column is `utf8mb4_unicode_ci`, so `LIKE` ignores case and accents: "Loghi", "LOGO" and "logos" are one word.
            // A file that is not a picture is a document whatever it was called: a PDF filed as "logo" would otherwise be
            // offered by the picker of the logos.
            migrationBuilder.Sql(MapSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to give back: the words that were there are not kept, and the ones written here are valid free text.
        }
    }
}
