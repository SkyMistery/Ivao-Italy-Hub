namespace IvaoHub.Core.Services;

/// <summary>
/// What one installation says about itself, as opposed to what the division says in
/// <c>division.json</c>: the same division runs a test installation and a production one, and they
/// differ here and nowhere else (note 2026-09-27-l-installazione-di-prova).
/// <para>Read from <c>secrets/*.json</c> or from <c>Installation__*</c> environment variables, like
/// <c>AllowedHosts</c>: it describes the server, not the division, and a fork writes it per server.</para>
/// </summary>
public sealed class InstallationOptions
{
    public const string SectionName = "Installation";

    /// <summary>
    /// The host of this installation, when it is not the division's (<c>test.hub.example.org</c>
    /// next to <c>hub.example.org</c>). It is folded into <c>DivisionOptions.Domain</c> when the
    /// options are built, so that there is one domain to read and every absolute link — a mail, the
    /// sitemap, robots.txt — is built on it. ⚠️ Never read this key: read the division's domain.
    /// </summary>
    public const string DomainKey = "Installation:Domain";

    /// <summary>
    /// A private installation: a test or a staging copy that must not be found and is open to the
    /// staff of the division only. robots.txt disallows everything, the sitemap does not exist, every
    /// response carries <c>X-Robots-Tag</c>, and a member who is neither staff nor super
    /// administrator is turned away at the end of the IVAO round trip, before anything about them is
    /// written. Off by default: an installation that says nothing is a public one.
    /// </summary>
    public bool Preview { get; init; }
}
