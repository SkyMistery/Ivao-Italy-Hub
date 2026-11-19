using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IvaoHub.Core.Content;

/// <summary>
/// Whether the browser would frame an address a <c>video</c> or an <c>embed</c> block holds, so that publication can stop
/// one it would not: on the public page the visitor would read the editor's warning in place of the block (issue #524;
/// Carmine, 18 November 2026, "one JSON", note 2026-11-18-l-indirizzo-che-l-hub-non-incorpora).
/// <para>⚠️ The list is not here: it is <c>web/src/blocks/embedHosts.json</c>, the one the blocks build their frame from,
/// compiled into this assembly. A site added there is a site this answers for, with nothing to copy. What is written on
/// both sides is the few rules that read an entry (<c>web/src/blocks/allowlist.ts</c> says them), and
/// <c>web/src/blocks/embedHosts.cases.json</c> is the table both are held to, here by <c>FramedAddressesTests</c>.</para>
/// <para>It only answers yes or no: the frame itself is the browser's to build.</para>
/// </summary>
public static partial class FramedAddresses
{
    private const string ResourceName = "IvaoHub.Core.Content.embedHosts.json";

    private static readonly Lazy<EmbedHostList> List = new(Load);

    /// <summary>
    /// Whether this address would be framed: <c>https</c>, on a host of the list, and read by the first of its shapes that
    /// applies, each name an identifier. An address that is empty, relative or not an address at all would not.
    /// </summary>
    public static bool Frames(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || AsTheBrowserReadsIt(address.Trim()) is not { } readable
            || !Uri.TryCreate(readable, UriKind.Absolute, out var url)
            || url.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = url.IdnHost.ToLowerInvariant();
        var site = List.Value.Sites.FirstOrDefault(candidate => candidate.Hosts.Any(name => Matches(host, name)));

        if (site is null)
        {
            return false;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var shape = site.Addresses.FirstOrDefault(candidate => Applies(candidate, host, parts));

        return shape is not null && ReadsEveryName(shape, url, parts);
    }

    /// <summary>
    /// The address with what <see cref="Uri"/> reads differently from the browser's <c>URL</c> taken care of, both found
    /// by the table of cases: a backslash before the query is a slash to the browser, so it is one here; and an escape
    /// before the query (<c>/%64Qw…</c>) the browser keeps, so it is never an identifier there, while <see cref="Uri"/>
    /// decodes one of a letter or a digit. Such an address is refused here: an identifier never holds a <c>%</c>, and
    /// the only address this can refuse that the browser would frame is one with an escape in a part nobody reads.
    /// </summary>
    private static string? AsTheBrowserReadsIt(string address)
    {
        var end = address.IndexOfAny(['?', '#']);
        var head = end < 0 ? address : address[..end];

        return head.Contains('%', StringComparison.Ordinal)
            ? null
            : head.Replace('\\', '/') + (end < 0 ? string.Empty : address[end..]);
    }

    /// <summary>A host is that name or one of its subdomains, and never <c>evil-youtube.com</c>.</summary>
    private static bool Matches(string host, string name) =>
        host == name || host.EndsWith($".{name}", StringComparison.Ordinal);

    private static bool Applies(AddressShape shape, string host, string[] parts) =>
        (shape.On is null || shape.On.Any(name => Matches(host, name)))
        && (shape.Path ?? []).Select((part, index) => Placeholder(part) is not null
            || (index < parts.Length && parts[index] == part)).All(matched => matched);

    private static bool ReadsEveryName(AddressShape shape, Uri url, string[] parts)
    {
        var identifier = List.Value.IdentifierPattern;
        var path = (shape.Path ?? []).Select((part, index) => (part, value: index < parts.Length ? parts[index] : null));
        var query = (shape.Query ?? new Dictionary<string, string>())
            .Select(pair => (part: pair.Value, value: QueryValue(url.Query, pair.Key)));

        return path.Concat(query)
            .Where(named => Placeholder(named.part) is not null)
            .All(named => named.value is not null && IsWhole(identifier.Match(named.value), named.value));
    }

    /// <summary>
    /// ⚠️ The whole value, not only what <c>$</c> lets through: in .NET it also matches before a final line break, which
    /// the browser's does not, and <c>abc%0A</c> would be framed here and refused there.
    /// </summary>
    private static bool IsWhole(Match match, string value) => match.Success && match.Length == value.Length;

    /// <summary><c>{id}</c> is <c>id</c>; anything else is a literal.</summary>
    private static string? Placeholder(string part)
    {
        var match = PlaceholderPattern().Match(part);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>The first value of a query parameter, decoded the way a browser decodes it, or <c>null</c>.</summary>
    private static string? QueryValue(string query, string name)
    {
        foreach (var pair in query.TrimStart('?').Split('&'))
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            var key = Decode(equals < 0 ? pair : pair[..equals]);

            if (key == name)
            {
                return Decode(equals < 0 ? string.Empty : pair[(equals + 1)..]);
            }
        }

        return null;
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));

    private static EmbedHostList Load()
    {
        using var stream = typeof(FramedAddresses).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing from the package: {ResourceName}.");

        var list = JsonSerializer.Deserialize<EmbedHostList>(stream, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException($"{ResourceName} is empty.");

        return list with { IdentifierPattern = new Regex(list.Identifier, RegexOptions.CultureInvariant) };
    }

    [GeneratedRegex(@"^\{(\w+)\}$", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();

    private sealed record EmbedHostList(string Identifier, IReadOnlyList<EmbedSite> Sites)
    {
        public Regex IdentifierPattern { get; init; } = new("(?!)");
    }

    private sealed record EmbedSite(string Key, IReadOnlyList<string> Hosts, IReadOnlyList<AddressShape> Addresses);

    private sealed record AddressShape(
        IReadOnlyList<string>? On,
        IReadOnlyList<string>? Path,
        IReadOnlyDictionary<string, string>? Query,
        string Frame);
}
