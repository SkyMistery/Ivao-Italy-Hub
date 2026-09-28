using System.Net;
using System.Net.Sockets;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace IvaoHub.Web.Endpoints;

/// <summary>
/// How the hub sees the request of whoever is looking: the neighbour it came from, the address and the scheme it believes,
/// and the forwarding headers exactly as they arrived (note 2026-09-28-l-indirizzo-del-visitatore-dietro-i-proxy, §8).
/// <para>The one way to read, on a real installation, what the proxies in front actually pass on. Nothing outside tells:
/// the audit log shows only the end of the chain, and the forwarded headers middleware writes nothing about a chain it
/// accepts. So the question "did <c>X-Forwarded-For</c> arrive, and was it believed?" gets an answer here, for every
/// installation and not only the first one.</para>
/// <para>Only a super administrator, only their own request, and nothing is kept: the answer is built from the request
/// and thrown away with it, never cached (<c>/api</c> is <c>no-store</c>), never written to a log. A JSON answer and not a
/// page: the values are header names and addresses, which no language translates, and whoever reads them copies them into
/// a report word for word.</para>
/// <para>No name of a provider is written here (<c>CLAUDE.md</c> §3). A header this list does not have — the one a CDN
/// sets with the visitor's address, say — is shown by naming it in <see cref="ExtraHeadersKey"/>; its name already
/// appears among <see cref="RequestDiagnosticsResponse.HeaderNames"/> if it arrived at all.</para>
/// </summary>
internal static class RequestDiagnosticsEndpoints
{
    /// <summary>Where the answer lives.</summary>
    public const string Pattern = "/api/admin/diagnostics/request";

    /// <summary>More headers whose value is shown, named by the installation.</summary>
    public const string ExtraHeadersKey = "Diagnostics:RequestHeaders";

    /// <summary>The headers a proxy uses to say who the caller was, and how it came in.</summary>
    private static readonly string[] ForwardingHeaders =
        ["X-Forwarded-For", "X-Forwarded-Proto", "X-Forwarded-Host", "X-Real-IP", "Forwarded"];

    /// <summary>What carries a credential: its value is never shown, whatever the configuration names.</summary>
    private static readonly string[] NeverShown = ["Cookie", "Authorization", "Proxy-Authorization"];

    /// <summary>
    /// Takes a copy of the request as it arrived, before the forwarded headers middleware rewrites it: that middleware
    /// replaces the address and the scheme, and takes out of <c>X-Forwarded-For</c> the entries it consumed. Only for
    /// this one address; every other request passes untouched.
    /// </summary>
    public static IApplicationBuilder UseRequestDiagnosticsCapture(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var shown = ForwardingHeaders
            .Concat(app.Configuration.GetSection(ExtraHeadersKey).Get<string[]>() ?? [])
            .Select(name => name.Trim())
            .Where(name => name.Length > 0 && !NeverShown.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(Pattern, StringComparison.OrdinalIgnoreCase))
            {
                context.Features.Set(RequestAsReceived.Capture(context, shown));
            }

            await next();
        });
    }

    public static void MapRequestDiagnosticsEndpoints(this WebApplication app, bool forwardedHeadersInPipeline)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Pattern, Results<Ok<RequestDiagnosticsResponse>, ForbidHttpResult> (
            HttpContext context,
            ICurrentUser user,
            IOptions<ForwardedHeadersOptions> forwarding) =>
        {
            // The same answer as the list of the super administrators: there is nothing above Permissions.Manage, and
            // what the proxies of the installation pass on is as much the system's business as who administers it.
            if (!user.IsSuperadmin)
            {
                return TypedResults.Forbid();
            }

            var received = context.Features.Get<RequestAsReceived>()
                ?? throw new InvalidOperationException(
                    $"The request reached {Pattern} without passing {nameof(UseRequestDiagnosticsCapture)}.");

            var options = forwarding.Value;
            var connection = context.Connection;

            // "Applied" is judged against the request as it arrived: a caller can send an X-Original-For of their own.
            bool Applied(string header) =>
                !received.HeaderNames.Contains(header, StringComparer.OrdinalIgnoreCase)
                && context.Request.Headers.ContainsKey(header);

            return TypedResults.Ok(new RequestDiagnosticsResponse(
                Neighbour: RequestAddress.Of(received.RemoteAddress, received.RemotePort),
                Believed: RequestAddress.Of(connection.RemoteIpAddress, connection.RemotePort),
                AddressForwarded: Applied(options.OriginalForHeaderName),
                SchemeReceived: received.Scheme,
                Scheme: context.Request.Scheme,
                SchemeForwarded: Applied(options.OriginalProtoHeaderName),
                IsHttps: context.Request.IsHttps,
                Host: received.Host,
                Headers: received.Headers,
                HeaderNames: received.HeaderNames,
                Forwarding: new ForwardingSettings(
                    InPipeline: forwardedHeadersInPipeline,
                    Headers: options.ForwardedHeaders.ToString(),
                    ForwardLimit: options.ForwardLimit,
                    TrustedNetworks: [.. options.KnownIPNetworks.Select(network => network.ToString())],
                    TrustedProxies: [.. options.KnownProxies.Select(proxy => proxy.ToString())],
                    ForwardedForHeaderName: options.ForwardedForHeaderName,
                    ForwardedProtoHeaderName: options.ForwardedProtoHeaderName)));
        })
            .RequireAuthorization(HubPolicies.SignedIn)
            .WithTags("Diagnostics")
            .WithName("RequestDiagnostics");
    }

    /// <summary>The request before anything in the pipeline changed it. Lives as long as the request.</summary>
    private sealed record RequestAsReceived(
        IPAddress? RemoteAddress,
        int RemotePort,
        string Scheme,
        string Host,
        IReadOnlyList<RequestHeader> Headers,
        IReadOnlyList<string> HeaderNames)
    {
        public static RequestAsReceived Capture(HttpContext context, IEnumerable<string> shown)
        {
            var headers = context.Request.Headers;

            return new RequestAsReceived(
                context.Connection.RemoteIpAddress,
                context.Connection.RemotePort,
                context.Request.Scheme,
                context.Request.Host.Value ?? string.Empty,
                [.. shown.Select(name => RequestHeader.Of(name, headers[name]))],
                [.. headers.Keys.Order(StringComparer.OrdinalIgnoreCase)]);
        }
    }
}

/// <summary>What the hub sees of one request. Every header is listed as it arrived, before the forwarded headers ran.</summary>
/// <param name="Neighbour">Who opened the connection: the last proxy, or the visitor if there is none.</param>
/// <param name="Believed">The address the hub works with: the audit log, the rate limit of the login.</param>
/// <param name="AddressForwarded">Whether the forwarded headers middleware replaced the address.</param>
/// <param name="SchemeReceived">The scheme of the hop from the neighbour.</param>
/// <param name="Scheme">The scheme the hub works with: HSTS, the redirection to https, the secure cookie.</param>
/// <param name="SchemeForwarded">Whether the forwarded headers middleware replaced the scheme.</param>
/// <param name="IsHttps">What HSTS and the redirection to https decide on.</param>
/// <param name="Host">The <c>Host</c> header as it arrived.</param>
/// <param name="Headers">The forwarding headers, and those the installation named, with their values as they arrived.</param>
/// <param name="HeaderNames">The name of every header that arrived, never a value.</param>
/// <param name="Forwarding">What the forwarded headers middleware was told to do.</param>
internal sealed record RequestDiagnosticsResponse(
    RequestAddress Neighbour,
    RequestAddress Believed,
    bool AddressForwarded,
    string SchemeReceived,
    string Scheme,
    bool SchemeForwarded,
    bool IsHttps,
    string Host,
    IReadOnlyList<RequestHeader> Headers,
    IReadOnlyList<string> HeaderNames,
    ForwardingSettings Forwarding);

/// <summary>An address with its family, because <c>::ffff:127.0.0.1</c> and <c>127.0.0.1</c> are not in the same network.</summary>
/// <param name="Address">As the hub writes it, in the audit log too; <c>null</c> when there is none.</param>
/// <param name="Family"><c>IPv4</c>, <c>IPv6</c>, <c>IPv4-mapped IPv6</c>, or <c>none</c>.</param>
/// <param name="Port">The port, <c>0</c> when a forwarded address came without one.</param>
internal sealed record RequestAddress(string? Address, string Family, int Port)
{
    public static RequestAddress Of(IPAddress? address, int port) => new(
        address?.ToString(),
        address switch
        {
            null => "none",
            { IsIPv4MappedToIPv6: true } => "IPv4-mapped IPv6",
            { AddressFamily: AddressFamily.InterNetworkV6 } => "IPv6",
            _ => "IPv4",
        },
        port);
}

/// <summary>A header as it arrived: every line, and how many comma separated entries they hold together.</summary>
internal sealed record RequestHeader(string Name, IReadOnlyList<string> Values, int Entries)
{
    public static RequestHeader Of(string name, StringValues values) => new(
        name,
        [.. values.Select(value => value ?? string.Empty)],
        values.Sum(value => (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length));
}

/// <summary>What the forwarded headers middleware was told to do, as it holds it.</summary>
/// <param name="InPipeline">Whether the middleware runs at all: only when <c>ForwardedHeaders:TrustedNetworks</c> lists a network.</param>
/// <param name="Headers">Which of the forwarded headers it applies.</param>
/// <param name="ForwardLimit">How many entries, from the right, it may consume; <c>null</c> is no limit.</param>
/// <param name="TrustedNetworks">The networks whose headers it believes.</param>
/// <param name="TrustedProxies">The single addresses whose headers it believes.</param>
/// <param name="ForwardedForHeaderName">The header it reads the address from.</param>
/// <param name="ForwardedProtoHeaderName">The header it reads the scheme from.</param>
internal sealed record ForwardingSettings(
    bool InPipeline,
    string Headers,
    int? ForwardLimit,
    IReadOnlyList<string> TrustedNetworks,
    IReadOnlyList<string> TrustedProxies,
    string ForwardedForHeaderName,
    string ForwardedProtoHeaderName);
