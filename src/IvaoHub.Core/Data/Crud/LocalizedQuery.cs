using System.Data;
using System.Linq.Expressions;
using IvaoHub.Core.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace IvaoHub.Core.Data.Crud;

/// <summary>
/// Reading one language out of a translated column, in SQL. A <see cref="Localized{T}"/> is a JSON
/// object on the row, so "search the title in the language the user is reading" is a JSON path
/// lookup; MariaDB can do it, EF Core cannot express it on a converted property, and the one place
/// that bridges the two is here (plan section F5 task 1, risk table section E).
/// </summary>
public static class LocalizedQuery
{
    /// <summary>
    /// Fallback mapping for the result of the two JSON functions, used when the provider has not
    /// given one to the arguments. <c>JSON_UNQUOTE</c> yields text, and text is what the search
    /// compares.
    /// </summary>
    private static readonly RelationalTypeMapping TextMapping = new StringTypeMapping("longtext", DbType.String);

    /// <summary>
    /// The text of a translated column in one language, as SQL. Only ever called inside a LINQ
    /// query: outside one it has no meaning, because the value lives in the database.
    /// </summary>
    /// <param name="field">The translated column.</param>
    /// <param name="jsonPath">A JSON path built by <see cref="PathFor"/>.</param>
    public static string? Text(Localized<string> field, string jsonPath) =>
        throw new InvalidOperationException(
            $"{nameof(LocalizedQuery)}.{nameof(Text)} is only translatable inside a LINQ query. "
            + "Read the value with Localized<T>.Resolve when the row is already in memory.");

    /// <summary>The JSON path of a language, quoted so that any language code is a legal path.</summary>
    public static string PathFor(string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        // A language code is two or three letters, optionally with a region: nothing to escape in
        // practice, and the quotes make that true by construction rather than by hope.
        return $"$.\"{locale.Replace("\"", string.Empty, StringComparison.Ordinal)}\"";
    }

    /// <summary>
    /// Teaches the model how to turn <see cref="Text"/> into
    /// <c>JSON_UNQUOTE(JSON_EXTRACT(column, path))</c>. Called once from
    /// <see cref="HubDbContext.OnModelCreating"/>; it adds no table and no column, so it needs no
    /// migration.
    /// </summary>
    public static void Register(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var method = typeof(LocalizedQuery).GetMethod(nameof(Text))!;

        var function = modelBuilder.HasDbFunction(method);
        function.HasParameter("field").HasStoreType("json");
        function.HasParameter("jsonPath").HasStoreType("varchar(64)");

        function.HasTranslation(arguments =>
        {
            // Both calls are given a text mapping explicitly: an expression in the SQL tree with
            // no type mapping cannot be compared, and neither MariaDB function has one of its own
            // to inherit. The mapping of the path argument is the right one when it is there,
            // because it is the string mapping the provider chose for this model.
            var text = arguments[1].TypeMapping ?? TextMapping;

            var extracted = new SqlFunctionExpression(
                "JSON_EXTRACT",
                arguments,
                nullable: true,
                argumentsPropagateNullability: [true, true],
                typeof(string),
                text);

            return new SqlFunctionExpression(
                "JSON_UNQUOTE",
                [extracted],
                nullable: true,
                argumentsPropagateNullability: [true],
                typeof(string),
                text);
        });
    }

    /// <summary>
    /// <c>entity =&gt; LocalizedQuery.Text(entity.Title, path)</c>, built from a plain property
    /// selector so that a caller writes <c>x =&gt; x.Title</c> and nothing else.
    /// </summary>
    internal static Expression<Func<TEntity, string?>> Selector<TEntity>(
        Expression<Func<TEntity, Localized<string>>> field,
        string jsonPath)
    {
        var call = Expression.Call(
            typeof(LocalizedQuery).GetMethod(nameof(Text))!,
            field.Body,
            Expression.Constant(jsonPath, typeof(string)));

        return Expression.Lambda<Func<TEntity, string?>>(call, field.Parameters);
    }
}
