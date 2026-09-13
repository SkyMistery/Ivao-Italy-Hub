using Microsoft.Extensions.Options;

namespace IvaoHub.Web;

/// <summary>
/// The headers every response carries, read from <c>config/security.json</c>.
///
/// <para>⚠️ Until 12 September 2026 this application sent **none of them**: no content security
/// policy, no <c>X-Content-Type-Options</c>, no <c>Referrer-Policy</c>, nothing. The gap was found
/// while designing the interactive block, which needs a frame with a policy of its own
/// (<c>docs/internal/decisions/2026-09-12-gli-header-di-sicurezza.md</c>).</para>
///
/// <para>The policy is a file and not code because two servers send it: this one in production,
/// where ASP.NET serves both the SPA and the API, and Vite's <c>preview</c> during the smoke suite,
/// so that all 58 of those tests run under the real policy rather than under nothing. One file,
/// two readers, and an integration test that reads the same file and checks what actually comes
/// back on the wire.</para>
/// </summary>
public sealed class SecurityHeadersOptions
{
    /// <summary>The plain headers: a name and a value, sent as they are written.</summary>
    public Dictionary<string, string> Headers { get; init; } = [];

    public ContentSecurityPolicyOptions ContentSecurityPolicy { get; init; } = new();
}

/// <summary>
/// The content security policy, as directives rather than as one long string: a fork that has to
/// add a host to <c>frame-src</c> edits a list, and cannot break the syntax of the rest.
/// </summary>
public sealed class ContentSecurityPolicyOptions
{
    /// <summary>The switch, and it exists for one reason: a policy that turns out to break a real
    /// installation must be removable without a rebuild, because production is reached by FTP and
    /// there is no shell there (plan section 2.5).</summary>
    public bool Enabled { get; init; }

    public Dictionary<string, string[]> Directives { get; init; } = [];

    /// <summary>The header value: <c>directive source source; directive source</c>.</summary>
    public string Compose() => string.Join(
        "; ",
        Directives.Select(directive => $"{directive.Key} {string.Join(' ', directive.Value)}"));
}

internal static class SecurityHeadersPipeline
{
    /// <summary>
    /// Sends the headers on every response, computed once at start up.
    ///
    /// <para>Early in the pipeline, so that a static file and an error page carry them too — and
    /// **before** the endpoints, deliberately: a response writes its headers when it starts, so an
    /// endpoint that needs a policy of its own can overwrite these after this line has run. That is
    /// how the frame of an interactive block will get <c>frame-ancestors 'self'</c> where the page
    /// around it says <c>'none'</c>.</para>
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<SecurityHeadersOptions>>().Value;
        var headers = options.Headers.ToArray();
        var policy = options.ContentSecurityPolicy.Enabled ? options.ContentSecurityPolicy.Compose() : null;

        return app.Use(async (context, next) =>
        {
            foreach (var (name, value) in headers)
            {
                context.Response.Headers[name] = value;
            }

            if (policy is not null)
            {
                context.Response.Headers.ContentSecurityPolicy = policy;
            }

            await next();
        });
    }
}
