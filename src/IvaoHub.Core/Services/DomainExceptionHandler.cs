using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Core.Services;

/// <summary>
/// Turns the two failures the domain raises on its own into the answers the API promises:
/// <see cref="ForbiddenDomainException"/>, which the save changes interceptor throws when a write
/// crosses a department, into 403; and a concurrency failure, which means somebody else saved the
/// row first, into 409 (design M0 sections 3.4 and 3.9).
/// <para>The interceptor is the net under the policies, so a 403 from here is a bug being caught
/// rather than the normal path — the CRUD engine refuses the same write earlier and more politely.
/// It is logged as a warning for that reason.</para>
/// </summary>
public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var (status, titleKey) = exception switch
        {
            ForbiddenDomainException => (StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, CrudProblems.ConflictTitleKey),
            _ => (0, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        if (exception is ForbiddenDomainException forbidden)
        {
            logger.LogWarning(
                "A write was stopped by the interceptor rather than by a policy: {Permission} on {Path}.",
                forbidden.Permission,
                httpContext.Request.Path);
        }

        var catalog = httpContext.RequestServices.GetRequiredService<LocaleCatalog>();
        var currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUser>();

        httpContext.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = catalog.Resolve(currentUser.Locale, titleKey),
            },
        });
    }
}
