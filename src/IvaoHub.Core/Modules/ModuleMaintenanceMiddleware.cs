using IvaoHub.Core.Auth;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Core.Modules;

/// <summary>
/// A module closed for maintenance still answers reads and refuses writes (design M0 section 6.1).
/// <para>Reading is deliberately left open: a department that is reorganising its data does not
/// want its pages to go blank, it wants nobody to change anything while it works. A job of the
/// module asks <c>IsInMaintenanceAsync</c> at the top of its run for the same reason.</para>
/// <para>It sits before routing, so a write to an address the module does not even have is refused
/// too: while a module is closed, nothing under its prefix accepts a change, whether or not that
/// particular address exists.</para>
/// </summary>
public static class ModuleMaintenanceMiddleware
{
    /// <summary>The i18n key the refusal carries, resolved from the same files as the SPA.</summary>
    public const string TitleKey = "errors.maintenance.title";

    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"];

    public static IApplicationBuilder UseModuleMaintenance(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var registry = context.RequestServices.GetRequiredService<ModuleRegistry>();
            var module = registry.ForApiPath(context.Request.Path);

            if (module is null
                || SafeMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase)
                || !await registry.IsInMaintenanceAsync(module.Key, context.RequestAborted))
            {
                await next();
                return;
            }

            var catalog = context.RequestServices.GetRequiredService<LocaleCatalog>();
            var currentUser = context.RequestServices.GetRequiredService<ICurrentUser>();
            var problems = context.RequestServices.GetRequiredService<IProblemDetailsService>();

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            await problems.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = catalog.Resolve(currentUser.Locale, TitleKey),
                    Extensions = { ["module"] = module.Key },
                },
            });
        });
    }
}
