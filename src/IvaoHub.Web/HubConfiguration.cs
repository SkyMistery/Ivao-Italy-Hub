using System.Net;
using System.Reflection;
using IvaoHub.Core.Services;

namespace IvaoHub.Web;

/// <summary>Reading the configuration files of an installation, in one place.</summary>
internal static class HubConfiguration
{
    /// <summary>
    /// The build time OpenAPI tool invokes this entry point in its own process in order to read
    /// the endpoints, so the application is being described rather than started: there is no
    /// database, no OAuth client and no port to bind, and there must be none (design M0 section
    /// 7.4). Recognised by the entry assembly, which is the tool and not this application; under
    /// the test host it is neither, and the check stays false as it must.
    /// </summary>
    public static bool IsOpenApiDocumentGeneration { get; } = string.Equals(
        Assembly.GetEntryAssembly()?.GetName().Name,
        "GetDocument.Insider",
        StringComparison.Ordinal);

    /// <summary>
    /// Every <c>*.json</c> under <c>secrets/</c>, in a stable order. The folder is never in the
    /// repository and the web server denies access to it (plan section 11.3).
    /// </summary>
    public static IEnumerable<string> SecretFiles(HubPaths paths)
    {
        if (!Directory.Exists(paths.Secrets))
        {
            return [];
        }

        return Directory.EnumerateFiles(paths.Secrets, "*.json").OrderBy(file => file, StringComparer.Ordinal);
    }

    /// <summary>
    /// The division file is loaded on its own so that its keys never mix with the settings of the
    /// application. Missing or unreadable, the application does not start.
    /// </summary>
    public static IConfiguration DivisionFile(HubPaths paths)
    {
        if (!File.Exists(paths.DivisionFile))
        {
            throw new InvalidOperationException(
                $"The division file is missing: {paths.DivisionFile}. "
                + "Copy config/division.example.json to config/division.json and fill it in.");
        }

        return new ConfigurationBuilder().AddJsonFile(paths.DivisionFile, optional: false).Build();
    }

    /// <summary>
    /// In production the host list must be explicit. Host header filtering is what keeps a request
    /// carrying a forged <c>Host</c> from being served at all: absolute links, cookies scoped by
    /// host and anything that echoes the host back are only as trustworthy as this list.
    /// <para>The OIDC redirect URI is deliberately not part of that: it is taken verbatim from
    /// <c>ivao-oauth.json</c> and never rebuilt from the request (design M0 section 4).</para>
    /// </summary>
    public static void RequireAllowedHosts(IConfiguration configuration)
    {
        var allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Split(';').Any(host => host.Trim() == "*"))
        {
            throw new InvalidOperationException(
                "'AllowedHosts' must list the real host names in production, without '*'. "
                + "Set it in a file under secrets/ or in the AllowedHosts environment variable, "
                + "for example \"hub.example.org;www.hub.example.org\".");
        }
    }

    /// <summary>Set to false when the proxy in front already refuses plain http itself.</summary>
    public const string RedirectToHttpsKey = "Https:Redirect";

    /// <summary>
    /// Whether the application answers a plain http request with a redirect to https.
    /// <para>On by default in production. It is safe there because the scheme is taken from
    /// <c>X-Forwarded-Proto</c> of a declared proxy (see <see cref="TrustedProxies"/>), so a request
    /// that reached the proxy over https is not redirected again: the loop that makes people afraid
    /// of this middleware comes from trusting nobody for that header, which this application no
    /// longer does. An installation whose proxy already redirects can turn it off.</para>
    /// </summary>
    public static bool RedirectToHttps(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetValue(RedirectToHttpsKey, defaultValue: true);
    }

    /// <summary>Configuration section holding the networks whose forwarded headers are believed.</summary>
    public const string TrustedProxiesKey = "ForwardedHeaders:TrustedNetworks";

    /// <summary>
    /// The proxies whose <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> may be believed, as
    /// CIDR networks.
    /// <para>This has to be spelled out, and the application refuses to start in production
    /// without it. Believing those headers from anybody means believing the client about its own
    /// address: the per IP rate limiter on the login is then bypassed by changing a header on
    /// every request, and the address in <c>hub_audit_log</c> becomes whatever the writer felt
    /// like. The list is the front doors of the installation — the Cloudflare ranges, or the
    /// address of the nginx in front — and nothing else.</para>
    /// </summary>
    public static IReadOnlyList<IPNetwork> TrustedProxies(IConfiguration configuration, bool required)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration.GetSection(TrustedProxiesKey).Get<string[]>() ?? [];
        var networks = new List<IPNetwork>();

        foreach (var entry in configured.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (!IPNetwork.TryParse(entry.Trim(), out var network))
            {
                throw new InvalidOperationException(
                    $"'{TrustedProxiesKey}' contains '{entry}', which is not a CIDR network such as "
                    + "\"173.245.48.0/20\" or \"2400:cb00::/32\".");
            }

            networks.Add(network);
        }

        if (networks.Count == 0 && required)
        {
            throw new InvalidOperationException(
                $"'{TrustedProxiesKey}' must list the networks of the proxies in front of this "
                + "installation, in CIDR form. Without it the application would believe the "
                + "X-Forwarded-For of any caller, which turns the rate limiting of the login and "
                + "the addresses in the audit log into something the caller chooses. Behind "
                + "Cloudflare, use the ranges Cloudflare publishes; behind a local reverse proxy, "
                + "its own address, for example \"127.0.0.1/32\".");
        }

        return networks;
    }
}
