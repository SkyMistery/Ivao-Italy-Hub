using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Jobs;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Web.E2E;

/// <summary>
/// What the end to end bench can ask besides signing in (M4, E10w; note <c>decisions/2026-10-18-il-banco-fa-girare-un-job.md</c>,
/// Carmine's answer B on issue #294): a job run now, and sessions on the network for one of its people. Without them a round
/// that waits for <c>events-roster</c> waits for its quarter of an hour, and a report of support of a person of the bench has
/// nothing to be checked against.
/// <para>Behind the same two locks as <see cref="E2ESignIn"/>, and in the same place: the environment <c>E2E</c> maps them, and
/// <c>E2E:Enabled</c> opens them. Neither alone does. A real installation has neither address.</para>
/// </summary>
internal static class E2EBenchRuns
{
    /// <summary>Where the bench runs a job now, by the name its rows in <c>hub_jobs_log</c> carry.</summary>
    public const string JobsPath = "/e2e/jobs/{name}";

    /// <summary>Where the bench gives one of its people sessions on the network, and takes them back.</summary>
    public const string SessionsPath = "/e2e/sessions/{vid:int}";

    /// <summary>The sessions the bench's people were given, which the fixture client reads (<see cref="BenchNetworkSessions"/>).</summary>
    public static IServiceCollection AddE2EBenchRuns(this IServiceCollection services)
    {
        services.AddSingleton<BenchNetworkSessions>();
        return services;
    }

    /// <summary>
    /// <c>POST /e2e/jobs/{name}</c> runs the job now and answers when it has ended, with what became of the run and the row it
    /// wrote; a name the hub has no job of is a 404. <c>PUT /e2e/sessions/{vid}</c> with <c>{ "atUtc": … }</c> gives a person of
    /// the bench the sessions of <see cref="FixtureIvaoApiClient.BenchTemplate"/>, the first starting then, and answers them as
    /// the network's client now reads them; <c>DELETE</c> takes them back. A VID that is none of the bench's people is a 404.
    /// </summary>
    public static void MapE2EBenchRuns(this WebApplication app)
    {
        // The second lock, as for the sign in: the environment carries the name, the configuration carries the intent.
        if (!app.Services.GetRequiredService<IOptions<E2EOptions>>().Value.Enabled)
        {
            return;
        }

        app.MapPost(JobsPath, async Task<Results<Ok<E2EJobRunResponse>, NotFound>> (
            string name,
            ScheduledJobs jobs,
            HubDbContext hub,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            // The row the run writes starts after this; the second it is in is enough, whatever the column keeps of it.
            var asked = clock.UtcNow;
            var from = asked.AddTicks(-(asked.Ticks % TimeSpan.TicksPerSecond));

            if (await jobs.RunNowAsync(name, cancellationToken) is not { } outcome)
            {
                return TypedResults.NotFound();
            }

            // A job that found nothing to do may write no row at all (the tours' checks): then there is none to answer.
            var run = await hub.JobsLog.AsNoTracking()
                .Where(row => row.Job == name && row.StartedAt >= from)
                .OrderByDescending(row => row.StartedAt)
                .ThenByDescending(row => row.Id)
                .Select(row => new E2EJobRow(row.StartedAt, row.FinishedAt, row.Status, row.Message))
                .FirstOrDefaultAsync(cancellationToken);

            return TypedResults.Ok(new E2EJobRunResponse(name, outcome, run));
        });

        app.MapPut(SessionsPath, async Task<Results<Ok<E2ESessionsResponse>, NotFound, ValidationProblem>> (
            int vid,
            E2ESessionsRequest request,
            IOptions<E2EOptions> e2e,
            BenchNetworkSessions bench,
            IIvaoApiClient ivao,
            CancellationToken cancellationToken) =>
        {
            if (!PeopleOf(e2e.Value).Contains(vid))
            {
                return TypedResults.NotFound();
            }

            if (request.AtUtc is not { } moment)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["atUtc"] = ["errors.required"],
                });
            }

            var at = moment.UtcDateTime;
            bench.Give(vid, at);

            // What the network's client answers for the person now, through the very path a module's question takes.
            var sessions = await ivao.SearchSessionsAsync(new IvaoSessionQuery(vid, at, at.AddDays(1)), cancellationToken) ?? [];
            return TypedResults.Ok(new E2ESessionsResponse(
                vid,
                [.. sessions
                    .OrderBy(session => session.StartedAt)
                    .Select(session => new E2ESession(
                        session.Id,
                        session.Callsign,
                        session.ConnectionType,
                        session.StartedAt,
                        session.EndedAt,
                        session.DepartureIcao,
                        session.ArrivalIcao))]));
        });

        app.MapDelete(SessionsPath, Results<NoContent, NotFound> (
            int vid,
            IOptions<E2EOptions> e2e,
            BenchNetworkSessions bench) =>
        {
            if (!PeopleOf(e2e.Value).Contains(vid))
            {
                return TypedResults.NotFound();
            }

            bench.Take(vid);
            return TypedResults.NoContent();
        });
    }

    /// <summary>The VIDs of the bench's people: the member of staff, and the pilot, the assistant, the trainer and the coordinator of the events it configured.</summary>
    private static HashSet<int> PeopleOf(E2EOptions options) =>
        [.. new E2EPersonOptions?[] { options, options.Pilot, options.Assistant, options.Trainer, options.Events }
            .OfType<E2EPersonOptions>()
            .Select(person => person.Vid)];
}

/// <summary>What became of a job the bench ran now, and the row it wrote, if it wrote one.</summary>
internal sealed record E2EJobRunResponse(string Job, JobRunState Outcome, E2EJobRow? Run);

/// <summary>The row of a run in <c>hub_jobs_log</c>.</summary>
internal sealed record E2EJobRow(DateTime StartedAt, DateTime? FinishedAt, string Status, string? Message);

/// <summary>When the first of the person's sessions starts.</summary>
internal sealed record E2ESessionsRequest(DateTimeOffset? AtUtc);

/// <summary>The sessions the network's client answers for the person, from the moment asked, the first first.</summary>
internal sealed record E2ESessionsResponse(int Vid, IReadOnlyList<E2ESession> Sessions);

/// <summary>One session as the network's client read it.</summary>
internal sealed record E2ESession(
    long Id,
    string Callsign,
    IvaoConnectionType? Connection,
    DateTime StartedAt,
    DateTime EndedAt,
    string? DepartureIcao,
    string? ArrivalIcao);
