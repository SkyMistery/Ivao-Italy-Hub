using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Web;
using IvaoHub.Web.Endpoints;
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

// The registry that knows the module keys arrives in F8; until then no key is checked.
builder.Services.AddSingleton<IValidateOptions<DivisionOptions>>(new DivisionOptionsValidator());

builder.Services.AddOptions<IvaoOAuthOptions>()
    .Bind(builder.Configuration.GetSection(IvaoOAuthOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<IvaoOAuthOptions>, IvaoOAuthOptionsValidator>();

// Persistent keys: losing them logs everybody out and makes the stored IVAO tokens unreadable,
// which the code treats as absent (plan section 16.14). They are never deleted by a deploy.
Directory.CreateDirectory(paths.DataProtectionKeys);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(paths.DataProtectionKeys))
    .SetApplicationName("IvaoHub");

// A localized field crosses the API as { "en": …, "it": … }: registered once, so no DTO has to
// remember it and every endpoint speaks the same shape.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new LocalizedJsonConverterFactory()));

builder.Services.AddHubDbContext();
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

if (builder.Environment.IsProduction())
{
    HubConfiguration.RequireAllowedHosts(builder.Configuration);
}

// Cloudflare and nginx sit in front, so the address of the caller arrives in a header. Which
// senders of that header are believed is declared, never "anybody": the login rate limiter and the
// address recorded in hub_audit_log both rest on the answer. In production the list is required.
var trustedProxies = HubConfiguration.TrustedProxies(
    builder.Configuration,
    required: builder.Environment.IsProduction());

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

// After the forwarded headers, never before: both of these judge the request by its scheme, and
// until the line above has run the scheme is the one of the hop from the proxy, not the one the
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

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapMeEndpoints();

app.MapGet("/api/version", (BuildInfo build) => Results.Ok(new
{
    version = build.Version,
    commit = build.Commit,
    builtAt = build.BuiltAt,
    dotnet = build.Dotnet,
}));

app.MapSpaFallback();

await app.InitializeAsync(paths);

app.Run();

/// <summary>Entry point marker, referenced by the integration tests through WebApplicationFactory.</summary>
public partial class Program;
