using System.Globalization;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Http;

namespace IvaoHub.Core.Auth;

/// <summary>
/// The version of a contract with a member's external program, the one a personal token is for (M4, E10g, note
/// 2026-10-06-la-versione-di-un-contratto-nel-nucleo). The hub has no <c>/api/v1</c> (plan §16 point 10): its pages and its
/// server ship together. A program is released on its own, so its contract says its version in a header of its own, and the
/// hub answers a request that says none, or one it does not speak, with 400 and the versions it accepts — assuming the
/// current one would move an old program to the next version in silence (Carmine: on 24 September 2026 for the tours'
/// agent, on 6 October 2026 for the events' export).
/// <para>A module declares its contract once and puts <see cref="RequireAsync"/> on the endpoints that speak it, with
/// <c>AddEndpointFilter(contract.RequireAsync)</c>. Within a version changes are only additive; a breaking one is the next
/// version, accepted beside the old one for at least one release. A program's writer reads the module's public document
/// (<c>docs/agent-contract.md</c> for the tours' agent, the first contract).</para>
/// </summary>
public sealed class ContractVersion
{
    /// <param name="header">The header the program says its version in, and the hub answers with: the contract's own.</param>
    /// <param name="current">The version the hub writes today, one of <paramref name="accepted"/>.</param>
    /// <param name="accepted">The versions the hub speaks, each once.</param>
    /// <param name="titleKey">The refusal's title, the module's key with its namespace: the core adds no word.</param>
    /// <param name="code">The refusal's <c>code</c>, the word the program acts on.</param>
    public ContractVersion(string header, int current, IReadOnlyList<int> accepted, string titleKey, string code)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentException.ThrowIfNullOrWhiteSpace(titleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        // A name that is not a token (RFC 9110 §5.6.2) cannot be a header: refused when the module declares the contract, not
        // found out by a program.
        if (header.Length == 0 || !header.All(IsTokenCharacter))
        {
            throw new ArgumentException($"'{header}' cannot be the name of a header.", nameof(header));
        }

        if (accepted.Count == 0 || accepted.Any(version => version < 1) || accepted.Distinct().Count() != accepted.Count)
        {
            throw new ArgumentException("A contract accepts at least one version, each a positive number, each once.", nameof(accepted));
        }

        if (!accepted.Contains(current))
        {
            throw new ArgumentOutOfRangeException(nameof(current), current, "The current version of a contract is one it accepts.");
        }

        Header = header;
        Current = current;
        Accepted = [.. accepted];
        TitleKey = titleKey;
        Code = code;
    }

    public string Header { get; }

    public int Current { get; }

    public IReadOnlyList<int> Accepted { get; }

    public string TitleKey { get; }

    public string Code { get; }

    /// <summary>
    /// The version a request speaks: the header trimmed, digits only, one of <see cref="Accepted"/>. Otherwise 400 with
    /// <see cref="Code"/>, <see cref="Current"/> and <see cref="Accepted"/>, titled in the asker's language. An accepted
    /// request is answered with the version the hub spoke, in the same header. An endpoint a program asks before it speaks
    /// any version — the tours' <c>/contract</c> — is left open and writes <see cref="Current"/> in the header itself.
    /// </summary>
    public async ValueTask<object?> RequireAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        var said = http.Request.Headers[Header].ToString().Trim();
        if (!int.TryParse(said, NumberStyles.None, CultureInfo.InvariantCulture, out var version) || !Accepted.Contains(version))
        {
            var catalog = http.RequestServices.GetService(typeof(LocaleCatalog)) as LocaleCatalog;
            var user = http.RequestServices.GetService(typeof(ICurrentUser)) as ICurrentUser;
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: catalog?.Resolve(user?.Locale ?? string.Empty, TitleKey) ?? TitleKey,
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["code"] = Code,
                    ["current"] = Current,
                    ["accepted"] = Accepted,
                });
        }

        http.Response.Headers[Header] = version.ToString(CultureInfo.InvariantCulture);
        return await next(context);
    }

    private static bool IsTokenCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character, StringComparison.Ordinal);
}
