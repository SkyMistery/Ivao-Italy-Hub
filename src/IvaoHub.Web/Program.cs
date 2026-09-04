using System.Text.Json.Serialization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Web;
using IvaoHub.Web.Endpoints;
using IvaoHub.Web.OpenApi;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Where config/, locales/, secrets/, hub-keys/, logs/ and diagnostics/ are. In production they sit
// next to the application; during development they are at the root of the repository.
var paths = HubPaths.Resolve(builder.Environment.ContentRootPath);

// Precedence: appsettings < secrets/*.json < config/ivao-oauth.json < environment variables.
foreach (var secretFile in HubConfiguration.SecretFiles(paths))
{
    builder.Configuration.AddJsonFile(secretFile, optional: true, reloadOnChange: true);
}

builder.Configuration.AddJsonFile(paths.OAuthFile, optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(paths.Logs, "hub-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true));

// The behaviour of the division is its own configuration file: it never mixes with the settings of
// the application, and a key of one can never shadow a key of the other.
builder.Services.AddSingleton(paths);

var divisionConfiguration = HubConfiguration.DivisionFile(paths);
builder.Services.AddOptions<DivisionOptions>()
    .Bind(divisionConfiguration)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Which modules exist is the explicit list, so the validator can say that division.json names one
// this build does not have. Every module, not only the enabled ones: naming a module in order to
// switch it off is the whole point of the key.
builder.Services.AddSingleton<IValidateOptions<DivisionOptions>>(provider => new DivisionOptionsValidator(
    [.. Modules.All.Select(module => module.Key)],
    provider.GetService<ILogger<DivisionOptionsValidator>>()));

var oauth = builder.Services.AddOptions<IvaoOAuthOptions>()
    .Bind(builder.Configuration.GetSection(IvaoOAuthOptions.SectionName));
builder.Services.AddSingleton<IValidateOptions<IvaoOAuthOptions>, IvaoOAuthOptionsValidator>();

if (!HubConfiguration.IsOpenApiDocumentGeneration)
{
    // An installation without an OAuth client must not come up. Describing the API is the one
    // case where there is no client to have: the tool reads the endpoints and never signs anybody in.
    oauth.ValidateOnStart();
}

// Persistent keys: losing them logs everybody out and makes the stored IVAO tokens unreadable,
// which the code treats as absent (plan section 16.14). They are never deleted by a deploy.
Directory.CreateDirectory(paths.DataProtectionKeys);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(paths.DataProtectionKeys))
    .SetApplicationName("IvaoHub");

// A localized field crosses the API as { "en": …, "it": … } and an enum as its name: registered
// once, so no DTO has to remember it and every endpoint speaks the same shape.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new LocalizedJsonConverterFactory());
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());

    // A number is a number. The web defaults also accept "5" for 5, which makes every integer in
    // the contract "integer or string" and every generated TypeScript field `number | string`;
    // our own client has no reason to send one, so the contract says so.
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

builder.Services.AddHubDbContext();
builder.Services.AddHubCrud();

// The modules of this build, and everything they contribute: permissions into the one catalogue,
// blocks into the one block registry, widgets into the one widget registry, their own services.
// It comes before the pieces that read those registries, and it is the only place the core is told
// that modules exist at all (design M0 section 6.1).
builder.Services.AddHubModules(
    Modules.All,
    builder.Configuration,
    divisionConfiguration.Get<DivisionOptions>() ?? new DivisionOptions());

// The block registry, the providers that answer for data blocks, publication and the seeder of the
// system templates. A module adds its blocks to the same registry, below.
builder.Services.AddHubContent();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<HubDatabaseInitializer>();
builder.Services.AddIvaoAuthentication();

// The FIRs and airports of the division, and the job that keeps their snapshot fresh. It reads
// nothing at this point: the time zone of the schedule and the choice between the real client and
// the fixtures are both resolved when the objects are built.
builder.Services.AddIvaoIntegration();

// The login is the one place an outsider can make the server do work before proving anything.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
        }));
});

// Strict transport security: written out rather than inherited, like the cookie flags.
// Thirty days and not a year, and without includeSubDomains: this hub is one host under a domain
// it shares with the rest of the division, and an HSTS policy is only as reversible as its longest
// max-age. Preload is deliberately absent for the same reason.
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(30);
    options.IncludeSubDomains = false;
    options.Preload = false;
});

builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddSingleton(BuildInfo.FromAssembly(typeof(Program).Assembly));

// The contract of the API, written to artifacts/openapi/ on every build and turned into the typed
// client of the SPA. The transformer is what tells the form generator that a field is localized.
builder.Services.AddOpenApi(options => options.AddSchemaTransformer(new LocalizedSchemaTransformer()));

if (HubConfiguration.IsOpenApiDocumentGeneration)
{
    // The tool starts the host to read the endpoints out of it, so an ephemeral loopback port
    // keeps a build out of the way of a development server that may well be running.
    builder.WebHost.UseUrls("http://127.0.0.1:0");
}

if (builder.Environment.IsProduction() && !HubConfiguration.IsOpenApiDocumentGeneration)
{
    HubConfiguration.RequireAllowedHosts(builder.Configuration);
}

// Cloudflare and nginx sit in front, so the address of the caller arrives in a header. Which
// senders of that header are believed is declared, never "anybody": the login rate limiter and the
// address recorded in hub_audit_log both rest on the answer. In production the list is required.
//
// Not while the OpenAPI document is being generated, though, and for the same reason AllowedHosts
// is exempt above: that tool runs this file to read the endpoints out of it, with no environment
// set and therefore as Production, on a machine that has no proxy in front of anything. Demanding
// a real deployment's configuration in order to write a JSON file would only mean a build that
// cannot run without production secrets.
var trustedProxies = HubConfiguration.TrustedProxies(
    builder.Configuration,
    required: builder.Environment.IsProduction() && !HubConfiguration.IsOpenApiDocumentGeneration);

if (trustedProxies.Count > 0)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var network in trustedProxies)
        {
            options.KnownIPNetworks.Add(network);
        }
    });
}

var app = builder.Build();

if (trustedProxies.Count > 0)
{
    app.UseForwardedHeaders();
}

// First of the three, so that everything below it leaves as problem details -- including a
// redirection that throws. The two the domain raises on its own become the 403 and the 409 the
// API promises.
app.UseExceptionHandler();

// After the forwarded headers, never before: both of these judge the request by its scheme, and
// until that line has run the scheme is the one of the hop from the proxy, not the one the
// browser used.
if (app.Environment.IsProduction())
{
    app.UseHsts();

    if (HubConfiguration.RedirectToHttps(app.Configuration))
    {
        app.UseHttpsRedirection();
    }
}

app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseNoStoreForApi();
app.UseRateLimiter();

app.UseDefaultFiles();
app.UseStaticFiles();

// Before authentication: a request that cannot prove it came from our own client is refused
// whatever it carries.
app.UseCrossSiteRequestGuard();

app.UseAuthentication();
app.UseAuthorization();

// A module closed for maintenance answers reads and refuses writes. After authentication so that
// the refusal is written in the language of whoever is asking, and before routing so that a write
// to an address the module does not even have is refused too.
app.UseModuleMaintenance();

app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    // A reader for the document while developing; the document itself is a build artefact.
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapLocaleEndpoints();
app.MapLinksEndpoints();
app.MapContentEndpoints();
app.MapBlockDataEndpoint();
app.MapSearchEndpoint();

// The administration of the hub itself: who holds which permission, who administers the system,
// what happened, and which modules are open. Three of the four are the CRUD engine in global mode.
app.MapGrantEndpoints();
app.MapSuperadminEndpoints();
app.MapAuditEndpoints();
app.MapModuleAdminEndpoints();

// Last, so that a module cannot shadow a route of the core by mapping the same pattern first.
app.MapModuleEndpoints();

app.MapGet("/api/version", (BuildInfo build) => TypedResults.Ok(
    new VersionResponse(build.Version, build.Commit, build.BuiltAt, build.Dotnet)));

app.MapSpaFallback();

// Migrations, super administrator bootstrap and diagnostics: everything that needs a database, and
// therefore everything the build time OpenAPI tool must not do.
if (!HubConfiguration.IsOpenApiDocumentGeneration)
{
    await app.InitializeAsync(paths);
}

app.Run();

/// <summary>What was deployed. Anonymous, and never cached, so a report can quote a build.</summary>
internal sealed record VersionResponse(string Version, string Commit, DateTime BuiltAt, string Dotnet);

/// <summary>Entry point marker, referenced by the integration tests through WebApplicationFactory.</summary>
public partial class Program;
