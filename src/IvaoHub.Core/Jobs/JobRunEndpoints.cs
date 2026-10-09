using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace IvaoHub.Core.Jobs;

/// <summary>
/// The address the scheduled task of the host calls (note 2026-10-09-i-job-che-recuperano, way E of
/// 2026-09-28-i-job-quando-passenger-spegne-l-hub, decided by Carmine on #165): a <c>POST</c> that runs, inside the
/// request, the jobs that are due — one after the other — and answers when they have ended. The host does not stop a
/// process while it answers, so the work is done by the time the answer leaves; the hours are the task's, never the code's.
/// </summary>
/// <remarks>
/// <para>It carries the installation's token (<see cref="JobOptions.Token"/>) as <c>Authorization: Bearer …</c>: a
/// secret of the installation and not of a person, which opens this address and nothing else, and whose only power is to
/// run now what is due anyway. An installation without a token has no such address (404); a call without the token, or
/// with another, is refused (401). The token is never written to the log. The host limits the calls as it limits the
/// login (<c>Program.cs</c>), so that refusals cannot fill the log.</para>
/// <para>It is a verb of the core outside every resource, like the diagnostics of the request: no row is read or written
/// here, the jobs write their own.</para>
/// </remarks>
public static class JobRunEndpoints
{
    /// <summary>Where the scheduled task calls.</summary>
    public const string Pattern = "/api/jobs/run";

    private const string BearerPrefix = "Bearer ";

    /// <summary>Maps the address; the host adds its limit to what this returns.</summary>
    public static RouteHandlerBuilder MapJobRunEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.MapPost(Pattern, async Task<Results<Ok<JobRunResponse>, UnauthorizedHttpResult, NotFound>> (
            HttpContext context,
            IOptions<JobOptions> options,
            ScheduledJobs jobs,
            ILoggerFactory loggers) =>
        {
            var token = options.Value.Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                return TypedResults.NotFound();
            }

            var logger = loggers.CreateLogger(typeof(JobRunEndpoints));
            if (!Carries(context.Request.Headers.Authorization, token))
            {
                logger.LogInformation("A call to {Pattern} without the installation's token was refused.", Pattern);
                context.Response.Headers.WWWAuthenticate = "Bearer";
                return TypedResults.Unauthorized();
            }

            var outcomes = await jobs.RunDueAsync(options.Value.Wait, context.RequestAborted);
            logger.LogInformation(
                "The scheduled task ran {Count} job(s) that were due: {Jobs}.",
                outcomes.Count,
                string.Join(", ", outcomes.Select(outcome => $"{outcome.Job} {outcome.Outcome}")));

            return TypedResults.Ok(new JobRunResponse(outcomes));
        })
            .AllowAnonymous()
            .WithTags("Jobs")
            .WithName("RunDueJobs");
    }

    /// <summary>
    /// Whether the request carries the token, compared in constant time on the hashes of the two, so that neither the
    /// length nor the first wrong character shows in how long a refusal takes.
    /// </summary>
    public static bool Carries(StringValues authorization, string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var header = authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var given = SHA256.HashData(Encoding.UTF8.GetBytes(header[BearerPrefix.Length..].Trim()));
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return CryptographicOperations.FixedTimeEquals(given, expected);
    }
}

/// <summary>What the scheduled task's call did.</summary>
/// <param name="Jobs">Each job that was due, in the order they start, and what became of it by the time the answer left.</param>
public sealed record JobRunResponse(IReadOnlyList<JobRunOutcome> Jobs);
