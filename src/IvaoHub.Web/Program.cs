var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Liveness probe. F1 replaces the fixed answer with a database ping.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Single-page application fallback. Everything that is not one of the prefixes below is
// answered with index.html so that the client-side router can take over.
// F8 replaces this hardcoded list with the one composed by the module registry
// (core exclusions plus IModule.SpaFallbackExclusions).
// Note: /login-error is a translated SPA route and therefore must NOT be excluded.
string[] spaFallbackExclusions =
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
    if (spaFallbackExclusions.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var index = app.Environment.WebRootFileProvider.GetFileInfo("index.html");

    if (!index.Exists)
    {
        // The SPA has not been published into wwwroot: in development it is served by Vite.
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(index);
});

app.Run();

/// <summary>Entry point marker, referenced by the integration tests through WebApplicationFactory.</summary>
public partial class Program;
