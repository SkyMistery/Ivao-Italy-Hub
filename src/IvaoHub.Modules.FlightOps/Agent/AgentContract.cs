using IvaoHub.Core.Auth;

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
    /// The title of the answer to a request without an accepted version, worded here. The server asks a module's key with
    /// its namespace, as the browser does (note 2026-09-26-le-chiavi-dei-moduli-con-il-namespace).
    /// </summary>
    public const string VersionTitleKey = "flightops:errors.agentContract";

    /// <summary>
    /// The version a request speaks, checked by the core (note 2026-10-06-la-versione-di-un-contratto-nel-nucleo): 400 with the
    /// accepted ones when it says none or one the hub does not speak (Carmine, 24 September 2026) — assuming the current one would
    /// move an old agent to version 2 in silence. Every answer says the version the hub spoke. <c>/contract</c> is left open and
    /// writes <see cref="Current"/> itself.
    /// </summary>
    public static readonly ContractVersion Version = new(Header, Current, Accepted, VersionTitleKey, "agentContract");
}
