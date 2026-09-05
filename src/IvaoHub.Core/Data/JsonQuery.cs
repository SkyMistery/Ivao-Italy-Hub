using System.Data;
using System.Globalization;
using IvaoHub.Core.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace IvaoHub.Core.Data;

/// <summary>
/// Asking a JSON column a question, in SQL. It is here for the same reason
/// <see cref="FullTextSearch"/> and <c>LocalizedQuery</c> are: a construct the provider only half
/// knows gets written once, is tested once, and is never copied into an endpoint with one detail
/// changed (implementation plan M1 section E).
/// <para>The question M1 needs is "which rows mention this identifier somewhere in their body?",
/// which is what stands between deleting a media and breaking a page that was already printed
/// (design M1 section 2).</para>
/// </summary>
public static class JsonQuery
{
    /// <summary>
    /// The JSON path of every value written under a given key, at any depth. <c>$**</c> is the
    /// recursive wildcard, so a key nested inside a section, a column and a block is found without
    /// this file knowing the shape of a block — which it must not, because block schemas exist only
    /// in TypeScript (plan section 16.5).
    /// </summary>
    public static string AnywhereUnder(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!key.All(char.IsLetterOrDigit))
        {
            // The path is not a parameter of the SQL statement — MariaDB wants it as a literal —
            // so the only safe key is one that cannot carry anything but itself.
            throw new ArgumentException("A JSON key of this hub is letters and digits.", nameof(key));
        }

        return $"$**.{key}";
    }

    /// <summary>The same, for a key whose value is an array: every element of it.</summary>
    public static string AnywhereUnderEach(string key) => $"{AnywhereUnder(key)}[*]";

    /// <summary>
    /// Whether the values a path selects contain this one. Only ever called inside a LINQ query:
    /// outside one it has no meaning, because the document lives in the database.
    /// </summary>
    /// <param name="document">The JSON column.</param>
    /// <param name="jsonPath">A path built by <see cref="AnywhereUnder"/>.</param>
    /// <param name="candidate">The value, written as JSON: <c>42</c> for a number.</param>
    public static bool Mentions(string document, string jsonPath, string candidate) =>
        throw new InvalidOperationException(
            $"{nameof(JsonQuery)}.{nameof(Mentions)} is only translatable inside a LINQ query.");

    /// <summary>
    /// Teaches the model how to turn <see cref="Mentions"/> into
    /// <c>JSON_CONTAINS(JSON_EXTRACT(document, path), candidate)</c>. Called once from
    /// <see cref="HubDbContext.OnModelCreating"/>; it adds no table and no column, so it needs no
    /// migration.
    /// </summary>
    public static void Register(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var method = typeof(JsonQuery).GetMethod(nameof(Mentions))!;

        var function = modelBuilder.HasDbFunction(method);
        function.HasParameter("document").HasStoreType("json");
        function.HasParameter("jsonPath").HasStoreType("varchar(64)");
        function.HasParameter("candidate").HasStoreType("varchar(64)");

        function.HasTranslation(arguments =>
        {
            var json = arguments[0].TypeMapping ?? JsonMapping;

            var extracted = new SqlFunctionExpression(
                "JSON_EXTRACT",
                [arguments[0], arguments[1]],
                nullable: true,
                argumentsPropagateNullability: [true, true],
                typeof(string),
                json);

            // JSON_CONTAINS answers 0 or 1, and NULL when the path selected nothing at all. Both
            // of the last two are "no", which is what a condition reads them as.
            return new SqlFunctionExpression(
                "JSON_CONTAINS",
                [extracted, arguments[2]],
                nullable: true,
                argumentsPropagateNullability: [true, true],
                typeof(bool),
                BoolMapping);
        });
    }

    /// <summary>
    /// The contents that use a media: the two columns that name one outright, and the body, where
    /// a block may hold it either as a single identifier or inside a list of them.
    /// <para>The key names are the convention a block follows, declared here and in
    /// <c>docs/UI-GUIDELINES.md</c> — the server cannot read a block schema, so this is the one
    /// thing the two sides agree on by name.</para>
    /// </summary>
    public static IQueryable<ContentEntry> UsingMedia(this IQueryable<ContentEntry> contents, long mediaId)
    {
        ArgumentNullException.ThrowIfNull(contents);

        var candidate = mediaId.ToString(CultureInfo.InvariantCulture);

        return contents.Where(content =>
            content.CoverMediaId == mediaId
            || content.FileMediaId == mediaId
            || Mentions(content.BodyJson, SingleKeyPath, candidate)
            || Mentions(content.BodyJson, ManyKeyPath, candidate));
    }

    /// <summary>
    /// The published versions that show a media. It is a different question from
    /// <see cref="UsingMedia"/>: a draft can be changed, a published page has already been read.
    /// </summary>
    public static IQueryable<ContentVersion> ShowingMedia(this IQueryable<ContentVersion> versions, long mediaId)
    {
        ArgumentNullException.ThrowIfNull(versions);

        var candidate = mediaId.ToString(CultureInfo.InvariantCulture);

        return versions.Where(version =>
            Mentions(version.BodyJson, SingleKeyPath, candidate)
            || Mentions(version.BodyJson, ManyKeyPath, candidate));
    }

    /// <summary>The property a block writes one media identifier into.</summary>
    public const string MediaKey = "mediaId";

    /// <summary>The property a block writes several into: a gallery, a grid of logos.</summary>
    public const string MediaListKey = "mediaIds";

    private static readonly string SingleKeyPath = AnywhereUnder(MediaKey);
    private static readonly string ManyKeyPath = AnywhereUnderEach(MediaListKey);

    private static readonly RelationalTypeMapping JsonMapping = new StringTypeMapping("json", DbType.String);
    private static readonly RelationalTypeMapping BoolMapping = new BoolTypeMapping("tinyint(1)", DbType.Boolean);
}
