using System.Data.Common;
using System.Diagnostics;
using IvaoHub.Core.Atc;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace IvaoHub.Web.Endpoints;

/// <summary>
/// Whether the hub reaches its two databases, asked on demand by a super administrator (note
/// 2026-10-16-la-prova-dei-collegamenti): its own, and the ATC archive when the division reads one. The archive is opened only
/// when a PIREP's validation or a roster proposal asks it something, so without this nobody can tell whether it works until
/// one of them answers «not available».
/// <para>Read only, and nothing is kept: a trivial query on the hub's database and the state of its migrations, the time of
/// the newest connection in the archive — never a row, never a person. A failure is answered with its <b>kind</b>, which the
/// page says in words; never the connection string, the user, the host or the message of the exception, which only the log
/// keeps, as it keeps any other failure of the database.</para>
/// <para>Beside <see cref="RequestDiagnosticsEndpoints"/>, with the same door: <c>SignedIn</c>, and
/// <see cref="ICurrentUser.IsSuperadmin"/> in the body. There is no download of a database here, and there never will be
/// one: the data are people's, and vIPI's database is read only through its views.</para>
/// </summary>
internal static class ConnectionCheckEndpoints
{
    /// <summary>Where the check is asked.</summary>
    public const string Pattern = "/api/admin/diagnostics/connections";

    /// <summary>
    /// How long one database is waited for, opening included. Longer than any healthy answer, shorter than the patience of
    /// whoever pressed the button; a connection string may ask for a longer connect timeout, and this one still wins.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    public static void MapConnectionCheckEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // A POST and not a GET: it is a test somebody runs, not a resource; nothing caches it, nothing prefetches it.
        app.MapPost(Pattern, async Task<Results<Ok<ConnectionCheckResponse>, ForbidHttpResult>> (
            ICurrentUser user,
            HubDbContext hub,
            IServiceProvider services,
            ModuleRegistry modules,
            IConfiguration configuration,
            IOptions<DivisionOptions> division,
            ILoggerFactory loggers,
            CancellationToken cancellationToken) =>
        {
            // The same answer as the request diagnostics: what the installation's databases say is the system's business.
            if (!user.IsSuperadmin)
            {
                return TypedResults.Forbid();
            }

            var logger = loggers.CreateLogger(typeof(ConnectionCheckEndpoints).FullName!);

            var hubCheck = await HubAsync(hub, services, modules, logger, cancellationToken);
            var archiveCheck = await ArchiveAsync(services, configuration, division.Value, logger, cancellationToken);

            return TypedResults.Ok(new ConnectionCheckResponse(hubCheck, archiveCheck));
        })
            .RequireAuthorization(HubPolicies.SignedIn)
            .WithTags("Diagnostics")
            .WithName("ConnectionCheck");
    }

    /// <summary>The hub's own database: a connection, <c>SELECT 1</c>, the server's version, and every context's migrations.</summary>
    private static async Task<HubDatabaseCheck> HubAsync(
        HubDbContext hub,
        IServiceProvider services,
        ModuleRegistry modules,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(Patience);

        try
        {
            string version;
            var connection = hub.Database.GetDbConnection();
            await hub.Database.OpenConnectionAsync(patience.Token);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(patience.Token);
                version = connection.ServerVersion;
            }
            finally
            {
                await hub.Database.CloseConnectionAsync();
            }

            List<MigrationState> migrations = [await MigrationsAsync(hub, patience.Token)];
            foreach (var contextType in modules.Enabled.SelectMany(module => module.DbContextTypes))
            {
                migrations.Add(await MigrationsAsync((DbContext)services.GetRequiredService(contextType), patience.Token));
            }

            return new HubDatabaseCheck(ConnectionOutcome.Ok, null, Elapsed(started), version, migrations);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception, cancellationToken))
        {
            var kind = Classify(exception, cancellationToken);
            logger.LogWarning(exception, "Connection check: the hub's database failed ({Kind}).", kind);
            return new HubDatabaseCheck(ConnectionOutcome.Failed, kind, Elapsed(started), null, []);
        }
    }

    /// <summary>What the history table of one context holds, against what the build carries.</summary>
    private static async Task<MigrationState> MigrationsAsync(DbContext context, CancellationToken cancellationToken)
    {
        var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var known = context.Database.GetMigrations().ToList();

        return new MigrationState(
            context.GetType().Name,
            known.LastOrDefault(applied.Contains),
            [.. known.Where(migration => !applied.Contains(migration))]);
    }

    /// <summary>
    /// The archive, only when the division reads one: through the core's source and its view, the newest connection's start.
    /// Without a connection string nothing is opened, and the answer says so.
    /// </summary>
    private static async Task<ArchiveCheck> ArchiveAsync(
        IServiceProvider services,
        IConfiguration configuration,
        DivisionOptions division,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (division.AtcData?.ReadsVipi != true)
        {
            return new ArchiveCheck(ConnectionOutcome.NotConfigured, null, null, null);
        }

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(AtcServiceCollectionExtensions.ConnectionStringName)))
        {
            return new ArchiveCheck(ConnectionOutcome.Failed, ConnectionError.NoConnectionString, null, null);
        }

        var started = Stopwatch.GetTimestamp();
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(Patience);

        try
        {
            var latest = await services.GetRequiredService<VipiAtcActivitySource>().LatestStartAsync(patience.Token);
            return new ArchiveCheck(ConnectionOutcome.Ok, null, Elapsed(started), latest);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception, cancellationToken))
        {
            var kind = Classify(exception, cancellationToken);
            logger.LogWarning(exception, "Connection check: the ATC archive failed ({Kind}).", kind);
            return new ArchiveCheck(ConnectionOutcome.Failed, kind, Elapsed(started), null);
        }
    }

    /// <summary>
    /// A failure of the database, or the patience running out; never the caller going away, which is not an answer to give.
    /// </summary>
    private static bool IsDatabaseFailure(Exception exception, CancellationToken request) =>
        !request.IsCancellationRequested
        && exception is DbException or InvalidOperationException or OperationCanceledException or TimeoutException;

    /// <summary>What went wrong, in one of the words the page knows: the first database error down the chain decides.</summary>
    internal static ConnectionError Classify(Exception exception, CancellationToken request = default)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException database)
            {
                return database.ErrorCode switch
                {
                    MySqlErrorCode.AccessDenied
                        or MySqlErrorCode.DatabaseAccessDenied
                        or MySqlErrorCode.TableAccessDenied
                        or MySqlErrorCode.ColumnAccessDenied => ConnectionError.AccessDenied,
                    MySqlErrorCode.UnknownDatabase => ConnectionError.UnknownDatabase,
                    MySqlErrorCode.NoSuchTable => ConnectionError.MissingView,
                    MySqlErrorCode.UnableToConnectToHost => ConnectionError.Unreachable,
                    MySqlErrorCode.CommandTimeoutExpired => ConnectionError.Timeout,
                    _ => ConnectionError.Other,
                };
            }

            if ((current is OperationCanceledException && !request.IsCancellationRequested) || current is TimeoutException)
            {
                return ConnectionError.Timeout;
            }
        }

        return ConnectionError.Other;
    }

    private static long Elapsed(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}

/// <summary>The two databases, as the check found them.</summary>
internal sealed record ConnectionCheckResponse(HubDatabaseCheck Database, ArchiveCheck Archive);

/// <summary>The hub's own database.</summary>
/// <param name="Outcome">Reached, or not.</param>
/// <param name="Error">The kind of the failure; <c>null</c> when reached.</param>
/// <param name="Milliseconds">How long the check took, opening, query and migrations together.</param>
/// <param name="ServerVersion">The server's own version string; <c>null</c> on a failure.</param>
/// <param name="Migrations">One line per context, the core's first; empty on a failure.</param>
internal sealed record HubDatabaseCheck(
    ConnectionOutcome Outcome,
    ConnectionError? Error,
    long Milliseconds,
    string? ServerVersion,
    IReadOnlyList<MigrationState> Migrations);

/// <summary>The migrations of one context.</summary>
/// <param name="Context">The context's type name, as the log and <c>startup.txt</c> name it.</param>
/// <param name="Latest">The newest migration of the build the database holds; <c>null</c> when it holds none.</param>
/// <param name="Pending">The migrations of the build the database does not hold yet: empty is up to date.</param>
internal sealed record MigrationState(string Context, string? Latest, IReadOnlyList<string> Pending);

/// <summary>The ATC archive.</summary>
/// <param name="Outcome">Reached, not reached, or not configured: the division reads no archive.</param>
/// <param name="Error">The kind of the failure; <c>null</c> otherwise.</param>
/// <param name="Milliseconds">How long the check took; <c>null</c> when nothing was opened.</param>
/// <param name="LatestSessionStart">The start of the newest connection in the archive; <c>null</c> when it holds none.</param>
internal sealed record ArchiveCheck(
    ConnectionOutcome Outcome,
    ConnectionError? Error,
    long? Milliseconds,
    DateTime? LatestSessionStart);

/// <summary>What the check found.</summary>
internal enum ConnectionOutcome
{
    Ok,
    Failed,
    NotConfigured,
}

/// <summary>The kinds of failure the page has words for. Never more than the kind: no message, no host, no user.</summary>
internal enum ConnectionError
{
    /// <summary>The user or its password was refused, or it may not read what it was asked.</summary>
    AccessDenied,

    /// <summary>The database named in the connection string does not exist.</summary>
    UnknownDatabase,

    /// <summary>The view (or table) the hub reads is not there.</summary>
    MissingView,

    /// <summary>No answer within ten seconds.</summary>
    Timeout,

    /// <summary>The server could not be reached at all.</summary>
    Unreachable,

    /// <summary>The division reads an archive whose connection string is not in the secrets.</summary>
    NoConnectionString,

    /// <summary>Anything else; the log has it.</summary>
    Other,
}
