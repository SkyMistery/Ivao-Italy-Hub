using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace IvaoHub.Web;

/// <summary>The pieces of the request pipeline and of the start up sequence, kept out of Program.</summary>
internal static class HubPipeline
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>What a start skips when the initialisation marker matches it, as <c>starts.txt</c> names the steps.</summary>
    public static readonly IReadOnlyList<string> SkippableSteps = ["migrations", "module migrations", "position grants", "content"];

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

            // A personal token is not a cookie: the browser never attaches it by itself, and a page of another site cannot
            // set an Authorization header on a cross site request without a preflight this host never answers (T19a).
            var bearer = context.Request.Headers.Authorization.ToString()
                .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

            if (guarded && !bearer && !safeMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
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
    /// What the core itself answers for and the single page application must therefore not swallow.
    /// A module adds its own through <c>IModule.SpaFallbackExclusions</c>, and the two are composed
    /// below: this list holds nothing that belongs to a module (design M0 section 6.4).
    /// <para>Note that <c>/login-error</c> is a translated SPA route and is deliberately absent.</para>
    /// </summary>
    private static readonly string[] CoreSpaFallbackExclusions =
    [
        "/api",
        "/auth/login",
        "/auth/callback",
        "/auth/logout",
        "/health",
        "/media",
        // The base map of the tours (T10): an archive of the world served by the hub, not a route of the SPA.
        TileEndpoints.Prefix,
        "/openapi",
        "/scalar",
        // The two files a crawler asks for. Without these the fallback would answer both with
        // index.html, and a search engine would read a page of JavaScript where it asked for XML.
        SeoEndpoints.SitemapPattern,
        SeoEndpoints.RobotsPattern,
    ];

    /// <summary>
    /// Serves index.html for everything the SPA router owns, and hands back to the server every
    /// prefix the core or a module claims.
    /// </summary>
    public static void MapSpaFallback(this WebApplication app)
    {
        var registry = app.Services.GetRequiredService<ModuleRegistry>();

        // The bench signs itself in over an address of its own, and only while it is the bench.
        string[] benchExclusions = app.Environment.IsEnvironment(HubEnvironments.E2E)
            ? [E2E.E2ESignIn.PathPrefix]
            : [];

        string[] exclusions = [.. CoreSpaFallbackExclusions, .. registry.SpaFallbackExclusions, .. benchExclusions];

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
    /// <remarks>
    /// Every step is timed into <paramref name="timings"/>, which <c>diagnostics/starts.txt</c> writes when the host is
    /// ready: the server's own numbers for the cold start a visitor pays (note 2026-09-28-l-avvio-a-freddo).
    /// <para>The steps in <see cref="SkippableSteps"/> run only when the initialisation marker does not match this start
    /// (note 2026-09-28-il-marcatore-d-inizializzazione); the others run at every start.</para>
    /// </remarks>
    public static async Task InitializeAsync(this WebApplication app, HubPaths paths, StartupTimings timings)
    {
        await using var scope = app.Services.CreateAsyncScope();

        // Fails here, with the list of the fields that are wrong, rather than on the first request.
        scope.ServiceProvider.GetRequiredService<IStartupValidator>().Validate();
        timings.Step("validation");

        // The permissions the entities say they are also written with, checked against the catalogue before anything is
        // written (M3, A3b): a mark the write guard could not honour stops the start here, rather than turning into a 403
        // nobody can explain. Building a model reads no table.
        var registry = scope.ServiceProvider.GetRequiredService<ModuleRegistry>();
        var catalogue = scope.ServiceProvider.GetRequiredService<PermissionCatalog>();
        foreach (var contextType in registry.Enabled.SelectMany(module => module.DbContextTypes).Prepend(typeof(HubDbContext)))
        {
            var model = ((DbContext)scope.ServiceProvider.GetRequiredService(contextType)).Model;
            catalogue.VerifyAlternatives(model.GetEntityTypes().Select(entity => entity.ClrType));
        }

        timings.Step("models");

        var division = scope.ServiceProvider.GetRequiredService<IOptions<DivisionOptions>>().Value;
        var build = scope.ServiceProvider.GetRequiredService<BuildInfo>();

        // A plain wake -- the same code, the same configuration, the same seed files as the last start that initialised
        // the database -- skips what that start already did (note 2026-09-28-il-marcatore-d-inizializzazione). The key
        // holds every input of the skipped steps; the mark is written only after all of them have succeeded.
        var key = InitialisationKey.Compute(
            build,
            [
                typeof(HubPipeline).Assembly,
                typeof(HubDbContext).Assembly,
                .. registry.All.Select(module => module.GetType().Assembly),
                .. registry.All.SelectMany(module => module.DbContextTypes).Select(type => type.Assembly),
            ],
            division,
            registry.EnabledKeys,
            app.Environment.EnvironmentName,
            paths.Seed);

        List<string> applied = [];
        var outcome = await scope.ServiceProvider.GetRequiredService<InitialisationMarker>().RunAsync(
            key,
            async cancellationToken =>
            {
                var initializer = scope.ServiceProvider.GetRequiredService<HubDatabaseInitializer>();
                applied.AddRange(await initializer.MigrateAsync(cancellationToken));
                timings.Step("migrations");

                // Then the contexts of the modules, each with its own migration history table, and like the core's only
                // when something is pending. A module with no table of its own -- the module the integration tests add --
                // declares none and nothing happens here.
                foreach (var contextType in registry.Enabled.SelectMany(module => module.DbContextTypes))
                {
                    var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
                    applied.AddRange(await initializer.MigrateAsync(context, cancellationToken));
                }

                timings.Step("module migrations");

                // The grants to positions the division starts with, applied once and then the table's (M2).
                await scope.ServiceProvider.GetRequiredService<PositionGrantSeeder>().SeedAsync(cancellationToken);
                timings.Step("position grants");

                // The system templates and the pages built from them, each applied once and never again:
                // a release may add one without undoing what the staff has done to the ones already there
                // (design M0 section 5.6, design M1 section 8.2).
                await scope.ServiceProvider.GetRequiredService<ContentSeeder>().SeedAsync(cancellationToken);
                timings.Step("content");
            },
            timings,
            app.Lifetime.ApplicationStopping);

        timings.Initialisation = outcome.Describe(SkippableSteps);

        // At every start, the mark or not: it reads division.json only when the database holds no super administrator at
        // all, and leaves an audit row whenever the effective set has moved (plan section 6.3) -- the set can be changed
        // in the database by hand, by anybody who reaches it, and this is where that becomes visible.
        await scope.ServiceProvider.GetRequiredService<SuperadminService>()
            .BootstrapAsync(app.Lifetime.ApplicationStopping);
        timings.Step("superadmins");

        // The first start of an installation has no airspace yet, and a hub that does not know its
        // own FIRs cannot recognise a FIR staff position. A failure here is a row in hub_jobs_log,
        // never a site that refuses to come up.
        if (!await scope.ServiceProvider.GetRequiredService<HubDbContext>().IvaoCenters.AnyAsync(
                app.Lifetime.ApplicationStopping))
        {
            await scope.ServiceProvider.GetRequiredService<RefDataSyncJob>()
                .RunAsync(app.Lifetime.ApplicationStopping);
        }

        timings.Step("reference data");

        await StartupDiagnostics.WriteAsync(
            paths,
            build,
            app.Environment.EnvironmentName,
            division.Code,
            applied,
            enabledModules: [.. registry.EnabledKeys],
            scope.ServiceProvider.GetRequiredService<IClock>().UtcNow,
            division.Domain,
            scope.ServiceProvider.GetRequiredService<IOptions<InstallationOptions>>().Value.Preview,
            app.Lifetime.ApplicationStopping);
        timings.Step("startup.txt");
    }
}
