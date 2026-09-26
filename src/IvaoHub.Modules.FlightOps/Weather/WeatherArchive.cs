using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Weather;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Settings;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IvaoHub.Modules.FlightOps.Weather;

/// <summary>
/// The weather the tours keep (design M2 §1.13; note 2026-09-15-meteo-e-confini-dei-fir §3.1), in one place: which airports
/// are watched, how a bulletin is saved without doubles, what a report's flight is missing at the send, how long a
/// bulletin stays, and what the validation page shows. The jobs and the send only call it.
/// <para>Only the airports the tours need, never the world: the legs of the tours open or closing, and at the send the
/// airports the flight touched that were not among them — a diversion, a tour without legs.</para>
/// </summary>
public sealed class WeatherArchive(
    FlightOpsDbContext database,
    IWeatherSource weather,
    ModuleSettingsStore settingsStore,
    ILogger<WeatherArchive> logger)
{
    /// <summary>
    /// The margin around a flight: a METAR is issued before the moment it describes, and the one an hour after landing
    /// still says what the pilot met.
    /// </summary>
    public static readonly TimeSpan FlightMargin = TimeSpan.FromHours(1);

    /// <summary>A flight without a landing — the tracker lost it — is looked at for this long after its take-off.</summary>
    public static readonly TimeSpan UnlandedFlight = TimeSpan.FromHours(12);

    /// <summary>
    /// How long before a flight a TAF may have been issued and still be the one in force: a TAF covers up to thirty hours,
    /// so the day before is kept with the flight's.
    /// </summary>
    public static readonly TimeSpan TafLookBack = TimeSpan.FromHours(30);

    /// <summary>
    /// The airports of the legs of the tours open or closing (§1.2.1), subtours included, retired legs left out: what the
    /// job asks for every half hour. A tour without legs — <c>Open</c> — adds none; its flights are filled in at the send.
    /// </summary>
    public async Task<IReadOnlyList<string>> WatchedAirportsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var tours = CrudSource.BackOffice<Tour>(database)
            .Where(TakesReports(now))
            .Select(tour => tour.Id);

        var legs = await CrudSource.BackOffice<Legs.Leg>(database).AsNoTracking()
            .Where(leg => tours.Contains(leg.TourId) && leg.RetiredAt == null)
            .Select(leg => new { leg.DepartureIcao, leg.ArrivalIcao })
            .ToListAsync(cancellationToken);

        return
        [
            .. legs.SelectMany(leg => new[] { leg.DepartureIcao, leg.ArrivalIcao })
                .Select(icao => icao.ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// How many days a bulletin nobody's report is waiting on is kept (<c>weatherRetentionDays</c>, Carmine, 24 September 2026):
    /// the longest report window of the tours open or closing, since a flight of today may be reported that many days from
    /// now; the division's default when no tour is.
    /// </summary>
    public async Task<int> RetentionDaysAsync(DateTime now, CancellationToken cancellationToken)
    {
        var longest = await CrudSource.BackOffice<Tour>(database)
            .Where(TakesReports(now))
            .MaxAsync(tour => (int?)tour.ReportWindowDays, cancellationToken);

        if (longest is { } days)
        {
            return days;
        }

        var settings = await settingsStore.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken);
        return settings.DefaultReportWindowDays;
    }

    /// <summary>
    /// Saves the bulletins that are not already kept — same airport, kind and moment — and returns how many were new. A
    /// send and the job saving the same bulletin at once is the one race: the unique index decides, and the loser saves
    /// the rest one by one.
    /// </summary>
    public async Task<int> SaveAsync(IReadOnlyCollection<WeatherReport> reports, DateTime now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reports);

        // On a whole second, as the sources give them: what the column keeps is what the next comparison reads.
        var fresh = reports
            .Where(report => !string.IsNullOrWhiteSpace(report.Raw) && report.Icao.Length is > 0 and <= 4)
            .GroupBy(report => (
                Icao: report.Icao.ToUpperInvariant(),
                report.Kind,
                IssuedAt: report.IssuedAt.AddTicks(-(report.IssuedAt.Ticks % TimeSpan.TicksPerSecond))))
            .Select(group => group.First() with { Icao = group.Key.Icao, IssuedAt = group.Key.IssuedAt })
            .ToList();

        if (fresh.Count == 0)
        {
            return 0;
        }

        var icaos = fresh.Select(report => report.Icao).Distinct().ToList();
        var earliest = fresh.Min(report => report.IssuedAt);
        var known = (await database.WeatherBulletins.AsNoTracking()
                .Where(row => icaos.Contains(row.Icao) && row.IssuedAt >= earliest)
                .Select(row => new { row.Icao, row.Kind, row.IssuedAt })
                .ToListAsync(cancellationToken))
            .Select(row => (row.Icao, row.Kind, row.IssuedAt))
            .ToHashSet();

        var rows = fresh
            .Where(report => !known.Contains((report.Icao, report.Kind, report.IssuedAt)))
            .Select(report => Row(report, now))
            .ToList();

        if (rows.Count == 0)
        {
            return 0;
        }

        database.WeatherBulletins.AddRange(rows);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return rows.Count;
        }
        catch (DbUpdateException)
        {
            ForgetBulletins();
        }

        var saved = 0;
        foreach (var row in rows)
        {
            database.WeatherBulletins.Add(Row(row, now));
            try
            {
                await database.SaveChangesAsync(cancellationToken);
                saved++;
            }
            catch (DbUpdateException)
            {
                // Saved by the other one in the meantime.
            }
            finally
            {
                ForgetBulletins();
            }
        }

        return saved;
    }

    /// <summary>
    /// Lets go of the bulletins the context holds, and of nothing else: at the send the context also tracks the report just
    /// saved, which the checks write on next — cleared with the rest, their suggestions and their time would not be saved.
    /// </summary>
    private void ForgetBulletins()
    {
        foreach (var entry in database.ChangeTracker.Entries<WeatherBulletin>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// At the send (design M2 §1.13 point 2): every airport of the report — the leg's, the flights', the diversion's — that
    /// has no METAR kept inside the flight is asked for its history over those hours, METARs and TAFs. An airport the job
    /// already watched is not asked again. It never throws and never takes long: the report is saved already, and a
    /// validator who finds no weather sees «not available», never a refused send.
    /// </summary>
    public async Task<int> FillFlightAsync(Pirep pirep, DateTime now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(20));

        try
        {
            var (from, to) = Window(pirep);
            var airports = Airports(pirep).Select(airport => airport.Icao).ToList();
            var covered = await database.WeatherBulletins.AsNoTracking()
                .Where(row => airports.Contains(row.Icao)
                    && row.Kind == WeatherReportKind.Metar
                    && row.IssuedAt >= from
                    && row.IssuedAt <= to)
                .Select(row => row.Icao)
                .Distinct()
                .ToListAsync(budget.Token);

            var saved = 0;
            foreach (var icao in airports.Except(covered, StringComparer.Ordinal))
            {
                var history = await weather.GetHistoryAsync(icao, from, to > now ? now : to, budget.Token);
                if (history is { Count: > 0 })
                {
                    saved += await SaveAsync(history, now, budget.Token);
                }
            }

            return saved;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The weather of report {Id} could not be filled in at the send.", pirep.Id);
            ForgetBulletins();
            return 0;
        }
    }

    /// <summary>
    /// What the validation page shows (§4.3): for each airport of the report, the METARs inside the flight and the TAFs in
    /// force during it — the last one issued before the take-off and any issued after — each with who published it.
    /// </summary>
    public async Task<IReadOnlyList<ReviewWeatherDto>> ForReviewAsync(Pirep pirep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        var (from, to) = Window(pirep);
        var airports = Airports(pirep);
        var icaos = airports.Select(airport => airport.Icao).ToList();
        var tafFrom = from - TafLookBack;

        var rows = await database.WeatherBulletins.AsNoTracking()
            .Where(row => icaos.Contains(row.Icao)
                && row.IssuedAt <= to
                && (row.Kind == WeatherReportKind.Metar ? row.IssuedAt >= from : row.IssuedAt >= tafFrom))
            .OrderBy(row => row.IssuedAt)
            .ToListAsync(cancellationToken);

        return
        [
            .. airports.Select(airport =>
            {
                var own = rows.Where(row => row.Icao == airport.Icao).ToList();
                var tafs = own.Where(row => row.Kind == WeatherReportKind.Taf).ToList();
                var inForce = tafs.LastOrDefault(row => row.IssuedAt <= from);

                return new ReviewWeatherDto(
                    airport.Icao,
                    airport.Role,
                    from,
                    to,
                    [.. own.Where(row => row.Kind == WeatherReportKind.Metar).Select(Dto)],
                    [.. tafs.Where(row => row.IssuedAt > from || row == inForce).Select(Dto)]);
            }),
        ];
    }

    /// <summary>
    /// The bulletins that may go (Carmine's rule, design M2 §1.13): older than <see cref="RetentionDaysAsync"/>, and no
    /// report still undecided — queued, in review, sent back, or rejected and disputed — with a flight that day on that
    /// airport. A decided report keeps nothing. Returns how many were deleted.
    /// </summary>
    public async Task<int> DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken)
    {
        var days = await RetentionDaysAsync(now, cancellationToken);

        // A whole day of margin: a bulletin of the morning belongs to a flight of the evening, reported the last day.
        var before = now.Date.AddDays(-(days + 1));

        var candidates = await database.WeatherBulletins.AsNoTracking()
            .Where(row => row.IssuedAt < before)
            .Select(row => new { row.Id, row.Icao, row.IssuedAt })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return 0;
        }

        var waiting = await CrudSource.BackOffice<Pirep>(database).AsNoTracking()
            .Where(report => report.Status == PirepStatus.Queued
                || report.Status == PirepStatus.InReview
                || report.Status == PirepStatus.ToModify
                || (report.Status == PirepStatus.Rejected && report.IsDisputed))
            // Only a report that flew near the line can hold a bulletin behind it: the TAF of the day before, a long flight.
            .Where(report => report.TakeoffAt < before.AddDays(3))
            .Include(report => report.Flights)
            .ToListAsync(cancellationToken);

        // Per airport, the days a waiting report flew there: a TAF of the day before stays with them.
        var held = waiting
            .SelectMany(report =>
            {
                var (from, to) = Window(report);
                return Airports(report).SelectMany(airport =>
                    Days(from - TafLookBack, to).Select(day => (airport.Icao, day)));
            })
            .ToHashSet();

        var ids = candidates
            .Where(row => !held.Contains((row.Icao, row.IssuedAt.Date)))
            .Select(row => row.Id)
            .ToList();

        foreach (var chunk in ids.Chunk(500))
        {
            database.WeatherBulletins.RemoveRange(chunk.Select(id => new WeatherBulletin { Id = id }));
            await database.SaveChangesAsync(cancellationToken);
            database.ChangeTracker.Clear();
        }

        return ids.Count;
    }

    /// <summary>From an hour before the first take-off to an hour after the last landing.</summary>
    public static (DateTime From, DateTime To) Window(Pirep pirep)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        if (pirep.Flights.Count == 0)
        {
            return (pirep.TakeoffAt - FlightMargin, pirep.TakeoffAt + UnlandedFlight);
        }

        var from = pirep.Flights.Min(flight => flight.TakeoffAt);
        var to = pirep.Flights.Max(flight => flight.LandingAt ?? flight.TakeoffAt + UnlandedFlight);
        return (from - FlightMargin, to + FlightMargin);
    }

    /// <summary>The airports of a report, each once, with the part it plays: the leg's, then the diversion, then the flights'.</summary>
    public static IReadOnlyList<(string Icao, WeatherAirportRole Role)> Airports(Pirep pirep)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        var all = new List<(string Icao, WeatherAirportRole Role)>
        {
            (pirep.DepartureIcao, WeatherAirportRole.Departure),
            (pirep.ArrivalIcao, WeatherAirportRole.Arrival),
        };

        if (pirep.DiversionIcao is { Length: > 0 } diversion)
        {
            all.Add((diversion, WeatherAirportRole.Diversion));
        }

        all.AddRange(pirep.Flights
            .OrderBy(flight => flight.Seq)
            .SelectMany(flight => new[] { flight.DepartureIcao, flight.ArrivalIcao })
            .Select(icao => (icao, WeatherAirportRole.Flown)));

        return
        [
            .. all.Where(airport => !string.IsNullOrWhiteSpace(airport.Icao))
                .Select(airport => (airport.Icao.ToUpperInvariant(), airport.Role))
                .DistinctBy(airport => airport.Item1),
        ];
    }

    private static System.Linq.Expressions.Expression<Func<Tour, bool>> TakesReports(DateTime now) =>
        tour => !tour.IsTemplate
            && tour.Status == PublishStatus.Published
            && tour.ReleaseAt != null
            && tour.ReleaseAt <= now
            && tour.CloseAt != null
            && tour.CloseAt.Value.AddDays(tour.ReportWindowDays) >= now;

    private static IEnumerable<DateTime> Days(DateTime from, DateTime to)
    {
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            yield return day;
        }
    }

    private static WeatherBulletin Row(WeatherReport report, DateTime now) => new()
    {
        Icao = report.Icao,
        Kind = report.Kind,
        IssuedAt = report.IssuedAt,
        Raw = report.Raw.Length <= WeatherBulletin.MaxRawLength ? report.Raw : report.Raw[..WeatherBulletin.MaxRawLength],
        Source = report.Source,
        FetchedAt = now,
    };

    private static WeatherBulletin Row(WeatherBulletin row, DateTime now) => new()
    {
        Icao = row.Icao,
        Kind = row.Kind,
        IssuedAt = row.IssuedAt,
        Raw = row.Raw,
        Source = row.Source,
        FetchedAt = now,
    };

    private static WeatherBulletinDto Dto(WeatherBulletin row) => new(row.Kind, row.IssuedAt, row.Raw, row.Source);
}

/// <summary>The part an airport plays in a report, for the validation page: the diversion's is shown with a reason <c>Weather</c>.</summary>
public enum WeatherAirportRole
{
    Departure,
    Arrival,
    Diversion,

    /// <summary>Touched by a flight and none of the above: the route of a flight after a diversion, say.</summary>
    Flown,
}

/// <summary>One kept bulletin, as the validator reads it.</summary>
public sealed record WeatherBulletinDto(WeatherReportKind Kind, DateTime IssuedAt, string Raw, string Source);

/// <summary>
/// The weather of one airport of a report during its flight (§4.3): empty lists mean nothing was kept — the page says
/// «not available», never «good weather».
/// </summary>
public sealed record ReviewWeatherDto(
    string Icao,
    WeatherAirportRole Role,
    DateTime From,
    DateTime To,
    IReadOnlyList<WeatherBulletinDto> Metars,
    IReadOnlyList<WeatherBulletinDto> Tafs);
