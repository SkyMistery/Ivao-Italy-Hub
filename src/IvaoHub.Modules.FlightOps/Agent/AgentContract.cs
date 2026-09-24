using System.Globalization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Http;

namespace IvaoHub.Modules.FlightOps.Agent;

/// <summary>
/// The contract with the agent on the validator's computer (design M2 §6.6; notes 2026-09-15-token-personali-e-agente-del-validatore
/// §3.2 and 2026-09-24-il-contratto-dell-agente). The version travels in a header, not in the address: the hub keeps its rule of
/// no <c>/api/v1</c>, because the page and the server ship together, while the agent is a second product released on its own.
/// <para>Within a version changes are only additive; a breaking one is version 2, accepted beside 1 for at least one release.
/// <c>docs/agent-contract.md</c> is what a writer of an agent reads.</para>
/// </summary>
public static class AgentContract
{
    /// <summary>The audience of the agent's personal tokens, and the permission a member needs to make one.</summary>
    public const string Audience = "flightops.agent";

    public const string Header = "Hub-Agent-Contract";

    public const int Current = 1;

    public static readonly IReadOnlyList<int> Accepted = [1];

    /// <summary>At most this many results in one request: every check of one report fits.</summary>
    public const int MaxResults = 50;

    /// <summary>The evidence of one result, all its lines together (note of 15 September §3.2).</summary>
    public const int MaxEvidenceCharacters = 2000;

    public const int MaxEvidenceLines = 50;

    public const int MaxVersionLength = 32;

    public const int MaxKeyLength = 64;

    /// <summary>
    /// The title of the answer to a request without an accepted version, worded here: the server's catalogue flattens the
    /// namespaces, so the key has none.
    /// </summary>
    public const string VersionTitleKey = "errors.agentContract";

    /// <summary>
    /// The version a request speaks: 400 with the accepted ones when it says none or one the hub does not speak (Carmine, 24
    /// September 2026) — assuming the current one would move an old agent to version 2 in silence. Every answer says the version
    /// the hub spoke.
    /// </summary>
    public static async ValueTask<object?> RequireVersionAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
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
                title: catalog?.Resolve(user?.Locale ?? string.Empty, VersionTitleKey) ?? VersionTitleKey,
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["code"] = "agentContract",
                    ["current"] = Current,
                    ["accepted"] = Accepted,
                });
        }

        http.Response.Headers[Header] = version.ToString(CultureInfo.InvariantCulture);
        return await next(context);
    }
}
