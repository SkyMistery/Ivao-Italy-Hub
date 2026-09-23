using System.Data.Common;
using IvaoHub.Core.Division;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Atc;

/// <summary>
/// The archive of vIPI, the division's ATC site, read through the view it exposes for the hub (note
/// 2026-09-14-dati-condivisi-con-vipi §3.3): <c>v_share_atc_sessions</c>, ten columns, with a MariaDB user of the hub's that
/// can do nothing but <c>SELECT</c> on it. The view is the contract — vIPI changes its tables underneath and the view only
/// ever gains columns — so nothing here knows how vIPI stores a session.
/// <para>vIPI keeps the division's positions from its start and the rest of the world from 28 August 2026, and prunes
/// sessions older than a year; neither date is written here: the oldest row of each half is asked, and that is where the
/// archive is complete from (<see cref="AtcActivity.Covers"/>).</para>
/// <para>Whatever goes wrong — no connection string, a server that does not answer, a view not yet created — the answer is
/// <see langword="null"/>, «not available», and the report is sent all the same.</para>
/// </summary>
public sealed class VipiAtcActivitySource(
    VipiShareDbContext database,
    IMemoryCache cache,
    IOptions<DivisionOptions> division,
    ILogger<VipiAtcActivitySource> logger) : IAtcActivitySource
{
    /// <summary>
    /// The longest a connection is believed to last: the bound that lets the query walk the index on the start instead of
    /// the whole archive. A connection open for longer than this before the interval is not seen.
    /// </summary>
    public static readonly TimeSpan LongestConnection = TimeSpan.FromDays(2);

    private const string CoverageKey = "atc:vipi:coverage";

    /// <summary>The oldest rows move once a night, when vIPI prunes; an hour is plenty.</summary>
    private static readonly TimeSpan CoverageLifetime = TimeSpan.FromHours(1);

    public async Task<AtcActivity?> OnlineAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        if (toUtc < fromUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        try
        {
            var coverage = await CoverageAsync(cancellationToken);
            var earliest = fromUtc - LongestConnection;

            var rows = await database.Sessions
                .Where(row => row.StartUtc <= toUtc && row.StartUtc >= earliest && (row.EndUtc == null || row.EndUtc >= fromUtc))
                .Select(row => new { row.Callsign, row.Frequency, row.StartUtc, row.EndUtc })
                .ToListAsync(cancellationToken);

            return new AtcActivity(
                [
                    .. rows.Select(row => new AtcPresence(
                        row.Callsign.Trim().ToUpperInvariant(),
                        string.IsNullOrWhiteSpace(row.Frequency) ? null : row.Frequency.Trim(),
                        DateTime.SpecifyKind(row.StartUtc, DateTimeKind.Utc),
                        row.EndUtc is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null)),
                ],
                coverage.Division,
                coverage.World,
                division.Value.IcaoPrefixes);
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning(exception, "The ATC archive could not be read; the controllers are «not available».");
            return null;
        }
    }

    private async Task<(DateTime? Division, DateTime? World)> CoverageAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CoverageKey, out (DateTime?, DateTime?) cached))
        {
            return cached;
        }

        var oldest = await database.Sessions
            .GroupBy(row => row.IsOutsideDivision)
            .Select(group => new { Outside = group.Key, Since = group.Min(row => row.StartUtc) })
            .ToListAsync(cancellationToken);

        static DateTime? Utc(DateTime? at) => at is { } value ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : null;

        (DateTime?, DateTime?) coverage = (
            Utc(oldest.FirstOrDefault(row => !row.Outside)?.Since),
            Utc(oldest.FirstOrDefault(row => row.Outside)?.Since));

        cache.Set(CoverageKey, coverage, CoverageLifetime);
        return coverage;
    }
}

/// <summary>
/// The view of vIPI's archive, read only: no migrations (the view is vIPI's, created by its own), no interceptor (nothing is
/// ever written), no tracking. Registered by <c>AddSharedViewContext</c> with a connection of its own.
/// </summary>
public sealed class VipiShareDbContext(DbContextOptions<VipiShareDbContext> options) : DbContext(options)
{
    /// <summary>The name of the view in vIPI's database, as its migration creates it.</summary>
    public const string SessionsView = "v_share_atc_sessions";

    public DbSet<SharedAtcSession> Sessions => Set<SharedAtcSession>();

    /// <summary>Nothing is ever written to another site's database (note 2026-09-14 §3.2).</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new InvalidOperationException("vIPI's archive is read only.");

    /// <inheritdoc cref="SaveChanges(bool)" />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("vIPI's archive is read only.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<SharedAtcSession>(session =>
        {
            session.HasNoKey();
            session.ToView(SessionsView);
        });
    }
}

/// <summary>One row of <c>v_share_atc_sessions</c>: the ten columns of the contract, named as the view names them.</summary>
public sealed class SharedAtcSession
{
    public long SessionId { get; set; }

    public int Vid { get; set; }

    public string Callsign { get; set; } = string.Empty;

    public string? Position { get; set; }

    public string? Frequency { get; set; }

    public DateTime StartUtc { get; set; }

    /// <summary>Null while the connection is still open.</summary>
    public DateTime? EndUtc { get; set; }

    public int DurationSeconds { get; set; }

    public int? Rating { get; set; }

    public bool IsOutsideDivision { get; set; }
}
