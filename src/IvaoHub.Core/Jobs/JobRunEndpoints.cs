using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace IvaoHub.Core.Jobs;

/// <summary>
/// The address the scheduled task of the host calls (note 2026-10-09-i-job-che-recuperano, way E of
/// 2026-09-28-i-job-quando-passenger-spegne-l-hub, decided by Carmine on #165): it runs, inside the request, the jobs that
/// are due — one after the other — and answers when they have ended. The host does not stop a process while it answers, so
/// the work is done by the time the answer leaves; the hours are the task's, never the code's.
/// </summary>
/// <remarks>
/// <para>It carries the installation's token (<see cref="JobOptions.Token"/>): a secret of the installation and not of a
/// person, which opens this address and nothing else, and whose only power is to run now what is due anyway. Two ways, with
/// the same token, the same answers and the same limit (Carmine, on #239): a <c>POST</c> with
/// <c>Authorization: Bearer …</c>, the one to use wherever the host's scheduled task can run a command; and a <c>GET</c>
/// with the token in the address (<see cref="TokenParameter"/>), the fallback for a panel that can only fetch an address.
/// That token ends up in the logs of the web server and of the proxies in front, never in the hub's own
/// (<see cref="MaskToken"/>).</para>
/// <para>An installation without a token has no such address (404); a call without the token, or with another, is refused
/// (401). The token is never written to the log. The host limits the calls as it limits the login (<c>Program.cs</c>), so
/// that refusals cannot fill the log.</para>
/// <para>It is a verb of the core outside every resource, like the diagnostics of the request: no row is read or written
/// here, the jobs write their own.</para>
/// </remarks>
public static class JobRunEndpoints
{
    /// <summary>Where the scheduled task calls.</summary>
    public const string Pattern = "/api/jobs/run";

    /// <summary>The parameter of the address that carries the token, for a panel that can only fetch an address.</summary>
    public const string TokenParameter = "token";

    /// <summary>What the hub's log writes in place of the token's value.</summary>
    public const string TokenMask = "***";

    private const string BearerPrefix = "Bearer ";

    /// <summary>Maps the address, both ways; the host adds its limit to what this returns, and so to both.</summary>
    public static RouteGroupBuilder MapJobRunEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var address = app.MapGroup(Pattern)
            .AllowAnonymous()
            .WithTags("Jobs");

        // The token in a header: the way to use wherever the scheduled task can run a command.
        address.MapPost(string.Empty, (
                HttpContext context,
                IOptions<JobOptions> options,
                ScheduledJobs jobs,
                ILoggerFactory loggers) =>
                RunAsync(context, options.Value, jobs, loggers, token => Carries(context.Request.Headers.Authorization, token)))
            .WithName("RunDueJobs");

        // The token in the address: the fallback, for a panel whose scheduled task can only fetch one.
        address.MapGet(string.Empty, (
                [FromQuery(Name = TokenParameter)] string? token,
                HttpContext context,
                IOptions<JobOptions> options,
                ScheduledJobs jobs,
                ILoggerFactory loggers) =>
                RunAsync(context, options.Value, jobs, loggers, expected => IsTheToken(token, expected)))
            .WithName("RunDueJobsFromTheAddress");

        return address;
    }

    /// <summary>
    /// Whether the request's <c>Authorization</c> header carries the token as a bearer, compared as
    /// <see cref="IsTheToken"/> compares.
    /// </summary>
    public static bool Carries(StringValues authorization, string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var header = authorization.ToString();
        return header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            && IsTheToken(header[BearerPrefix.Length..], token);
    }

    /// <summary>
    /// Whether <paramref name="given"/> is the token, compared in constant time on the hashes of the two, so that neither
    /// the length nor the first wrong character shows in how long a refusal takes.
    /// </summary>
    public static bool IsTheToken(string? given, string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (string.IsNullOrWhiteSpace(given))
        {
            return false;
        }

        var hashGiven = SHA256.HashData(Encoding.UTF8.GetBytes(given.Trim()));
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return CryptographicOperations.FixedTimeEquals(hashGiven, expected);
    }

    /// <summary>
    /// The query string with the value of every <see cref="TokenParameter"/> written as <see cref="TokenMask"/>, for the
    /// hub's log: ASP.NET Core's own lines of a request write the query string whole whenever their level lets them through.
    /// The parameter is found as the address reads it, its name decoded and in any case, so that no spelling of it passes.
    /// Null when the query string carries no token.
    /// </summary>
    public static string? MaskToken(string? queryString)
    {
        if (string.IsNullOrEmpty(queryString))
        {
            return null;
        }

        var start = queryString.StartsWith('?') ? 1 : 0;
        var pairs = queryString[start..].Split('&');
        var masked = false;

        for (var index = 0; index < pairs.Length; index++)
        {
            var equals = pairs[index].IndexOf('=', StringComparison.Ordinal);
            if (equals < 0)
            {
                continue;
            }

            var name = pairs[index][..equals];
            if (string.Equals(Uri.UnescapeDataString(name.Replace('+', ' ')), TokenParameter, StringComparison.OrdinalIgnoreCase))
            {
                pairs[index] = $"{name}={TokenMask}";
                masked = true;
            }
        }

        return masked ? queryString[..start] + string.Join('&', pairs) : null;
    }

    private static async Task<Results<Ok<JobRunResponse>, UnauthorizedHttpResult, NotFound>> RunAsync(
        HttpContext context,
        JobOptions options,
        ScheduledJobs jobs,
        ILoggerFactory loggers,
        Func<string, bool> carriesTheToken)
    {
        var token = options.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            return TypedResults.NotFound();
        }

        var logger = loggers.CreateLogger(typeof(JobRunEndpoints));
        if (!carriesTheToken(token))
        {
            logger.LogInformation(
                "A {Method} to {Pattern} without the installation's token was refused.",
                context.Request.Method,
                Pattern);
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return TypedResults.Unauthorized();
        }

        var outcomes = await jobs.RunDueAsync(options.Wait, context.RequestAborted);
        logger.LogInformation(
            "The scheduled task ran {Count} job(s) that were due: {Jobs}.",
            outcomes.Count,
            string.Join(", ", outcomes.Select(outcome => $"{outcome.Job} {outcome.Outcome}")));

        return TypedResults.Ok(new JobRunResponse(outcomes));
    }
}

/// <summary>What the scheduled task's call did.</summary>
/// <param name="Jobs">Each job that was due, in the order they start, and what became of it by the time the answer left.</param>
public sealed record JobRunResponse(IReadOnlyList<JobRunOutcome> Jobs);
