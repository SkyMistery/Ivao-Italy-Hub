using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace IvaoHub.Web;

/// <summary>The pieces of the request pipeline and of the start up sequence, kept out of Program.</summary>
internal static class HubPipeline
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>What the generated client puts in X-Requested-With on every mutation.</summary>
    public const string RequestedWithValue = "hub";

    /// <summary>
    /// Everything a browser or Cloudflare could cache from the API is explicitly not cacheable.
    /// The static files of the SPA keep their own, normal caching.
    /// </summary>
    public static IApplicationBuilder UseNoStoreForApi(this WebApplication app)
    {
        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers.CacheControl = "no-store";
            }

            await next();
        });
    }

    /// <summary>
    /// Refuses any state changing call that does not carry the header our own client always sends.
    /// A cross site form can post to us with the cookie attached, but it cannot set a header
    /// (plan section 6.4). SameSite=Lax already covers most of it; this is the second lock.
    /// </summary>
    public static IApplicationBuilder UseCrossSiteRequestGuard(this WebApplication app)
    {
        string[] safeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"];

        return app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            var guarded = path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/auth/logout", StringComparison.OrdinalIgnoreCase);

            if (guarded && !safeMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
            {
                var header = context.Request.Headers.XRequestedWith.ToString();
                if (!string.Equals(header, RequestedWithValue, StringComparison.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }

            await next();
        });
    }

    /// <summary>One identifier per request, in the logs and in the response, to match a report to a log line.</summary>
    public static IApplicationBuilder UseCorrelationId(this WebApplication app)
    {
        return app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = context.TraceIdentifier;
            }

            context.Response.Headers[CorrelationIdHeader] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next();
            }
        });
    }

    /// <summary>
    /// Serves index.html for everything the SPA router owns. The list of prefixes it must not
    /// touch is hardcoded until F8 composes it from the module registry.
    /// Note: <c>/login-error</c> is a translated SPA route and is deliberately not excluded.
    /// </summary>
    public static void MapSpaFallback(this WebApplication app)
    {
        string[] exclusions =
        [
            "/api",
            "/auth/login",
            "/auth/callback",
            "/auth/logout",
            "/health",
            "/openapi",
            "/scalar",
            "/services/vsop",
            "/vsop",
            "/_content",
            "/_framework",
        ];

        app.MapFallback(async context =>
        {
            var path = context.Request.Path;
            if (exclusions.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var index = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
            if (!index.Exists)
            {
                // The SPA has not been published into wwwroot: in development Vite serves it.
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(index);
        });
    }

    /// <summary>
    /// Validates the configuration, applies the migrations and writes the diagnostics file, all
    /// before the first request is served. The order matters: a bad configuration must stop the
    /// application before it touches the database, and a half migrated database is worse than a
    /// site that is down and says why.
    /// </summary>
    public static async Task InitializeAsync(this WebApplication app, HubPaths paths)
    {
        await using var scope = app.Services.CreateAsyncScope();

        // Fails here, with the list of the fields that are wrong, rather than on the first request.
        scope.ServiceProvider.GetRequiredService<IStartupValidator>().Validate();

        var initializer = scope.ServiceProvider.GetRequiredService<HubDatabaseInitializer>();
        var applied = await initializer.MigrateAsync(app.Lifetime.ApplicationStopping);

        // Reads division.json only when the database holds no super administrator at all, and
        // leaves an audit row whenever the effective set has moved (plan section 6.3).
        await scope.ServiceProvider.GetRequiredService<SuperadminService>()
            .BootstrapAsync(app.Lifetime.ApplicationStopping);

        var division = scope.ServiceProvider.GetRequiredService<IOptions<DivisionOptions>>().Value;

        await StartupDiagnostics.WriteAsync(
            paths,
            scope.ServiceProvider.GetRequiredService<BuildInfo>(),
            app.Environment.EnvironmentName,
            division.Code,
            applied,
            enabledModules: [],
            app.Lifetime.ApplicationStopping);
    }
}
