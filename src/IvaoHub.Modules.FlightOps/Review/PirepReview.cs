using System.Globalization;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Settings;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.FlightOps.Review;

/// <summary>What a step of the review came to: done, refused field by field, or not the reader's to take.</summary>
public enum ReviewResult
{
    Done,
    Refused,
    Forbidden,
}

/// <summary>
/// The staff's side of a report (design M2 §4): reading it with everything the decision needs, taking it with a lease,
/// letting it go, deciding it with the errors marked among the rules it froze, and reopening a decision. Every write is a
/// write of the report, which the interceptor lets a validator enabled on the tour make (<c>AlsoWrittenWith</c>); who may
/// do what on a report is the one handler's answer, asked on the row (§7.3) — nobody decides their own reports, super
/// administrator included.
/// <para>The mails go after the save, through the one notification service, and never carry who decided (§3.5).</para>
/// </summary>
public sealed class PirepReview(
    FlightOpsDbContext database,
    HubDbContext hub,
    IAirportDirectory airports,
    IAuthorizationService authorization,
    IHttpContextAccessor http,
    INotificationService notifications,
    LocaleCatalog catalog,
    ModuleSettingsStore settingsStore,
    IOptions<DivisionOptions> division,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>A report with everything the staff reads on it: flights, errors, history. The tracks are asked for apart.</summary>
    public Task<Pirep?> FindAsync(long id, bool tracked, CancellationToken cancellationToken)
    {
        var reports = CrudSource.BackOffice<Pirep>(database)
            .Include(report => report.Flights)
            .Include(report => report.Errors)
            .Include(report => report.Events);
        return (tracked ? reports : reports.AsNoTracking())
            .AsSplitQuery()
            .FirstOrDefaultAsync(report => report.Id == id && report.Status != PirepStatus.Withdrawn, cancellationToken);
    }

    /// <summary>Whether the reader may take and decide this report: enabled on its tour, and not its pilot (§7.3).</summary>
    public async Task<bool> MayValidateAsync(Pirep pirep) =>
        http.HttpContext is { } context
        && (await authorization.AuthorizeAsync(context.User, pirep, TourPermissions.Validate)).Succeeded;

    /// <summary>
    /// Whether the reader may reopen its decision (§4.2.1): whoever took it, still enabled on the tour, or who holds
    /// <see cref="TourPermissions.ReopenDecisions"/> on it — never its pilot.
    /// </summary>
    public async Task<bool> MayReopenAsync(Pirep pirep)
    {
        if (!IsDecided(pirep.Status) || http.HttpContext is not { } context)
        {
            return false;
        }

        return (pirep.DecidedByVid == currentUser.Vid && await MayValidateAsync(pirep))
            || (await authorization.AuthorizeAsync(context.User, pirep, TourPermissions.ReopenDecisions)).Succeeded;
    }

    /// <summary>Whether the reader could take it now: theirs to validate, and waiting, or held by nobody any more, or by them.</summary>
    public bool IsTakable(Pirep pirep, DateTime now) =>
        pirep.Status == PirepStatus.Queued
        || (pirep.Status == PirepStatus.InReview && (pirep.AssignedToVid == currentUser.Vid || pirep.LeaseUntil is not { } until || until <= now));

    /// <summary>Takes a report (§4.2): <c>InReview</c> in the reader's hands for the lease. Taking one's own again renews it.</summary>
    public async Task<(ReviewResult Result, IReadOnlyDictionary<string, string[]>? Problems)> TakeAsync(
        Pirep pirep,
        DateTime rowVersion,
        CancellationToken cancellationToken)
    {
        if (!await MayValidateAsync(pirep))
        {
            return (ReviewResult.Forbidden, null);
        }

        var now = clock.UtcNow;
        if (!IsTakable(pirep, now))
        {
            return Refuse("status", pirep.Status == PirepStatus.InReview ? "flightops:errors.reviewTaken" : "flightops:errors.reviewNotWaiting");
        }

        var settings = await settingsStore.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken);
        database.Entry(pirep).Property(row => row.RowVersion).OriginalValue = rowVersion;

        if (pirep.Status != PirepStatus.InReview || pirep.AssignedToVid != currentUser.Vid)
        {
            Step(pirep, PirepStatus.InReview, now, "flightops:events.taken");
        }

        pirep.Status = PirepStatus.InReview;
        pirep.AssignedToVid = currentUser.Vid;
        pirep.LeaseUntil = now.AddMinutes(settings.LeaseMinutes);
        await database.SaveChangesAsync(cancellationToken);

        return (ReviewResult.Done, null);
    }

    /// <summary>Lets a report go back to the queue, for somebody who took the wrong one: only whoever holds it.</summary>
    public async Task<(ReviewResult Result, IReadOnlyDictionary<string, string[]>? Problems)> ReleaseAsync(
        Pirep pirep,
        DateTime rowVersion,
        CancellationToken cancellationToken)
    {
        if (!await MayValidateAsync(pirep))
        {
            return (ReviewResult.Forbidden, null);
        }

        if (pirep.Status != PirepStatus.InReview || pirep.AssignedToVid != currentUser.Vid)
        {
            return Refuse("status", "flightops:errors.reviewNotYours");
        }

        database.Entry(pirep).Property(row => row.RowVersion).OriginalValue = rowVersion;
        Step(pirep, PirepStatus.Queued, clock.UtcNow, "flightops:events.released");
        pirep.Status = PirepStatus.Queued;
        pirep.AssignedToVid = null;
        pirep.LeaseUntil = null;
        await database.SaveChangesAsync(cancellationToken);

        return (ReviewResult.Done, null);
    }

    /// <summary>
    /// Decides a report the reader holds (§4.3): accepted, to modify or rejected, with the errors marked among those of the
    /// rules it froze — they replace the ones of any decision before (§4.2.1). Going against the suggestion needs a reason.
    /// The pilot is told by mail, without the name of who decided.
    /// </summary>
    public async Task<(ReviewResult Result, IReadOnlyDictionary<string, string[]>? Problems)> DecideAsync(
        Pirep pirep,
        ReviewDecisionDto decision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (!await MayValidateAsync(pirep))
        {
            return (ReviewResult.Forbidden, null);
        }

        var problems = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (pirep.Status != PirepStatus.InReview || pirep.AssignedToVid != currentUser.Vid)
        {
            problems["status"] = ["flightops:errors.reviewNotYours"];
            return (ReviewResult.Refused, problems);
        }

        if (decision.Outcome is not (PirepStatus.Accepted or PirepStatus.ToModify or PirepStatus.Rejected))
        {
            problems["outcome"] = ["flightops:errors.reviewOutcome"];
        }

        var frozen = PirepSubmission.Snapshot(pirep)
            .SelectMany(rule => rule.Errors)
            .DistinctBy(error => error.Id)
            .ToDictionary(error => error.Id);
        var ids = (decision.ErrorIds ?? []).Distinct().ToList();
        if (ids.Any(id => !frozen.ContainsKey(id)))
        {
            problems["errorIds"] = ["flightops:errors.reviewErrorUnknown"];
        }
        else if (decision.Outcome == PirepStatus.Rejected && ids.Count == 0)
        {
            // The pilot is told which rules they broke (§3.5): a rejection without any would tell them nothing.
            problems["errorIds"] = ["flightops:errors.reviewRejectNeedsError"];
        }

        var noteToPilot = Trimmed(decision.NoteToPilot);
        if (decision.Outcome == PirepStatus.ToModify && noteToPilot is null)
        {
            problems["noteToPilot"] = ["flightops:errors.reviewToModifyNeedsNote"];
        }

        var suggestion = ReviewSuggestion.Of(
            ids.Where(frozen.ContainsKey).Select(id => frozen[id]),
            await CountsAsync(pirep, inYearOnly: true, cancellationToken));
        var overridden = ReviewSuggestion.Overrides(decision.Outcome, suggestion);
        var overrideReason = Trimmed(decision.OverrideReason);
        if (overridden && overrideReason is null)
        {
            problems["overrideReason"] = ["flightops:errors.reviewOverrideNeedsReason"];
        }

        foreach (var (field, value) in new[] { ("noteToPilot", decision.NoteToPilot), ("staffNote", decision.StaffNote), ("overrideReason", decision.OverrideReason) })
        {
            if (value?.Length > PirepValidation.MaxTextLength)
            {
                problems[field] = ["errors.text.tooLong"];
            }
        }

        if (problems.Count > 0)
        {
            return (ReviewResult.Refused, problems);
        }

        var now = clock.UtcNow;
        database.Entry(pirep).Property(row => row.RowVersion).OriginalValue = decision.RowVersion;

        database.PirepErrors.RemoveRange(pirep.Errors);
        pirep.Errors = [.. ids.Select(id => new PirepError { ErrorId = id, Category = frozen[id].Category, Confirmed = true })];

        Step(pirep, decision.Outcome, now, null);
        pirep.Status = decision.Outcome;
        pirep.DecidedByVid = currentUser.Vid;
        pirep.DecidedAt = now;
        pirep.AssignedToVid = null;
        pirep.LeaseUntil = null;
        pirep.NoteToPilot = noteToPilot;
        pirep.StaffNote = Trimmed(decision.StaffNote);
        pirep.ThresholdOverridden = overridden;
        pirep.OverrideReason = overridden ? overrideReason : null;
        await database.SaveChangesAsync(cancellationToken);

        await TellThePilotAsync(pirep, cancellationToken);
        return (ReviewResult.Done, null);
    }

    /// <summary>
    /// Reopens a decision (§4.2.1): the report is <c>InReview</c> again in the reader's hands, with the reason in its history.
    /// The next decision replaces this one, in the counts and in a new mail to the pilot.
    /// </summary>
    public async Task<(ReviewResult Result, IReadOnlyDictionary<string, string[]>? Problems)> ReopenAsync(
        Pirep pirep,
        ReviewReopenDto reopen,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reopen);

        if (!IsDecided(pirep.Status))
        {
            return Refuse("status", "flightops:errors.reviewNotDecided");
        }

        if (!await MayReopenAsync(pirep))
        {
            return (ReviewResult.Forbidden, null);
        }

        var reason = Trimmed(reopen.Reason);
        if (reason is null || reason.Length > PirepValidation.MaxTextLength)
        {
            return Refuse("reason", reason is null ? "errors.required" : "errors.text.tooLong");
        }

        var now = clock.UtcNow;
        var settings = await settingsStore.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken);
        database.Entry(pirep).Property(row => row.RowVersion).OriginalValue = reopen.RowVersion;

        Step(pirep, PirepStatus.InReview, now, reason);
        pirep.Status = PirepStatus.InReview;
        pirep.AssignedToVid = currentUser.Vid;
        pirep.LeaseUntil = now.AddMinutes(settings.LeaseMinutes);
        await database.SaveChangesAsync(cancellationToken);

        return (ReviewResult.Done, null);
    }

    /// <summary>The validation page of a report (§4.3).</summary>
    public async Task<ReviewDto> PageAsync(Pirep pirep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        var now = clock.UtcNow;
        var tour = await CrudSource.BackOffice<Tour>(database).AsNoTracking().FirstAsync(row => row.Id == pirep.TourId, cancellationToken);
        var slug = tour.Slug ?? await ParentSlugAsync(tour, cancellationToken) ?? string.Empty;

        var rules = PirepSubmission.Snapshot(pirep);
        var inYear = await CountsAsync(pirep, inYearOnly: true, cancellationToken);
        var ever = await CountsAsync(pirep, inYearOnly: false, cancellationToken);
        var marked = pirep.Errors.ToDictionary(error => error.ErrorId);

        var errors = rules
            .SelectMany(rule => rule.Errors.Select(error => (Rule: rule, Error: error)))
            .GroupBy(entry => entry.Error.Id)
            .Select(group => new ReviewErrorDto(
                group.Key,
                group.First().Error.Name,
                group.First().Error.Category,
                group.First().Error.YearlyMax,
                [.. group.Select(entry => entry.Rule.Code).Distinct()],
                inYear.GetValueOrDefault(group.Key),
                ever.GetValueOrDefault(group.Key),
                marked.TryGetValue(group.Key, out var error) && error.Confirmed,
                error?.SuggestedByCheck ?? false))
            .OrderByDescending(entry => entry.Category)
            .ThenBy(entry => entry.Id)
            .ToList();

        var suggestion = ReviewSuggestion.Of(
            rules.SelectMany(rule => rule.Errors).Where(frozen => marked.TryGetValue(frozen.Id, out var row) && row.Confirmed),
            inYear);

        var tracked = await database.PirepTracks.AsNoTracking()
            .Where(track => pirep.Flights.Select(flight => flight.Id).Contains(track.PirepFlightId))
            .Select(track => track.PirepFlightId)
            .ToListAsync(cancellationToken);

        var names = await NamesAsync(
            [pirep.Vid, pirep.AssignedToVid, pirep.DecidedByVid, .. pirep.Events.Select(step => (int?)step.ByVid)],
            cancellationToken);
        var (contacts, exemptions) = PirepSubmission.Atc(pirep);
        var leg = PirepSubmission.LegSnapshot(pirep);
        var codes = new[] { leg.DepartureIcao, leg.ArrivalIcao, pirep.DiversionIcao }
            .Concat(pirep.Flights.SelectMany(flight => new[] { flight.DepartureIcao, flight.ArrivalIcao }))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var located = await airports.FindAsync(codes, cancellationToken);
        var mayValidate = await MayValidateAsync(pirep);

        return new ReviewDto(
            pirep.Id,
            pirep.TourId,
            tour.Title,
            slug,
            pirep.Status,
            pirep.IsDisputed,
            pirep.Vid == currentUser.Vid,
            Member(pirep.Vid, names)!,
            pirep.SubmittedAt,
            pirep.ResubmittedAt,
            pirep.QueuedAt,
            leg,
            [.. codes.Select(code => located.GetValueOrDefault(code)).OfType<AirportDto>()],
            pirep.FlightRules,
            pirep.Sid,
            pirep.Star,
            pirep.Approach,
            pirep.IsDiversion,
            pirep.DiversionIcao,
            pirep.DiversionReason,
            pirep.DiversionNote,
            pirep.PilotRemarks,
            contacts,
            exemptions,
            pirep.AtcArchiveAvailable,
            [
                .. pirep.Flights.OrderBy(flight => flight.Seq).Select(flight => new ReviewFlightDto(
                    flight.Seq,
                    flight.TrackerSessionId,
                    flight.Callsign,
                    flight.Aircraft,
                    flight.DepartureIcao,
                    flight.ArrivalIcao,
                    flight.TakeoffAt,
                    flight.LandingAt,
                    Plans(flight.FlightPlansJson),
                    flight.PlanAtTakeoffRevision,
                    tracked.Contains(flight.Id))),
            ],
            rules,
            errors,
            suggestion,
            await ProfileAsync(pirep, names, cancellationToken),
            Member(pirep.AssignedToVid, names),
            pirep.LeaseUntil,
            Member(pirep.DecidedByVid, names),
            pirep.DecidedAt,
            pirep.NoteToPilot,
            pirep.StaffNote,
            pirep.ThresholdOverridden,
            pirep.OverrideReason,
            WeatherAvailable: false,
            ChecksAvailable: false,
            [
                .. pirep.Events.OrderBy(step => step.At).ThenBy(step => step.Id).Select(step => new ReviewEventDto(
                    step.FromStatus,
                    step.ToStatus,
                    step.ByVid == 0 ? null : Member(step.ByVid, names),
                    step.At,
                    step.Note)),
            ],
            new ReviewActionsDto(
                CanTake: mayValidate && IsTakable(pirep, now) && !(pirep.Status == PirepStatus.InReview && pirep.AssignedToVid == currentUser.Vid && pirep.LeaseUntil > now),
                CanRelease: mayValidate && pirep.Status == PirepStatus.InReview && pirep.AssignedToVid == currentUser.Vid,
                CanDecide: mayValidate && pirep.Status == PirepStatus.InReview && pirep.AssignedToVid == currentUser.Vid,
                CanReopen: await MayReopenAsync(pirep)),
            pirep.RowVersion);
    }

    /// <summary>What the suggestion would be with these errors marked, errors not among the frozen ones left out.</summary>
    public async Task<SuggestionDto> SuggestAsync(Pirep pirep, IReadOnlyCollection<long> errorIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pirep);
        ArgumentNullException.ThrowIfNull(errorIds);

        return ReviewSuggestion.Of(
            PirepSubmission.Snapshot(pirep).SelectMany(rule => rule.Errors).Where(error => errorIds.Contains(error.Id)),
            await CountsAsync(pirep, inYearOnly: true, cancellationToken));
    }

    /// <summary>The tracks of the report's flights, decoded; none for a flight whose track was never stored or has gone.</summary>
    public async Task<IReadOnlyList<ReviewTrackDto>> TracksAsync(Pirep pirep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        var ids = pirep.Flights.Select(flight => flight.Id).ToList();
        var tracks = await database.PirepTracks.AsNoTracking()
            .Where(track => ids.Contains(track.PirepFlightId))
            .ToDictionaryAsync(track => track.PirepFlightId, cancellationToken);

        return
        [
            .. pirep.Flights.OrderBy(flight => flight.Seq).Select(flight => new ReviewTrackDto(
                flight.Seq,
                tracks.TryGetValue(flight.Id, out var track) ? TrackCodec.Decode(track) : null)),
        ];
    }

    /// <summary>The names the staff reads next to VIDs; a member who never signed in has none.</summary>
    public async Task<IReadOnlyDictionary<int, string>> NamesAsync(IEnumerable<int?> vids, CancellationToken cancellationToken)
    {
        var wanted = vids.OfType<int>().Where(vid => vid > 0).Distinct().ToList();
        return (await hub.Users.AsNoTracking()
                .Where(user => wanted.Contains(user.Vid))
                .Select(user => new { user.Vid, user.FirstName, user.LastName })
                .ToListAsync(cancellationToken))
            .ToDictionary(user => user.Vid, user => $"{user.FirstName} {user.LastName}".Trim());
    }

    public static MemberDto? Member(int? vid, IReadOnlyDictionary<int, string> names) =>
        vid is { } known ? new MemberDto(known, names.TryGetValue(known, out var name) && name.Length > 0 ? name : null) : null;

    private static bool IsDecided(PirepStatus status) =>
        status is PirepStatus.Accepted or PirepStatus.ToModify or PirepStatus.Rejected;

    /// <summary>
    /// How many times the pilot has each error on their decided reports — accepted and rejected, on any tour — in the UTC
    /// year of this flight's take-off, or ever; this report left out (note 2026-09-23-la-validazione §2.5).
    /// </summary>
    private async Task<Dictionary<long, int>> CountsAsync(Pirep pirep, bool inYearOnly, CancellationToken cancellationToken)
    {
        var from = new DateTime(pirep.TakeoffAt.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);

        var decided = CrudSource.BackOffice<Pirep>(database)
            .Where(report => report.Vid == pirep.Vid
                && report.Id != pirep.Id
                && (report.Status == PirepStatus.Accepted || report.Status == PirepStatus.Rejected));
        if (inYearOnly)
        {
            decided = decided.Where(report => report.TakeoffAt >= from && report.TakeoffAt < to);
        }

        return await database.PirepErrors.AsNoTracking()
            .Where(error => error.Confirmed && decided.Select(report => report.Id).Contains(error.PirepId))
            .GroupBy(error => error.ErrorId)
            .Select(group => new { ErrorId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.ErrorId, row => row.Count, cancellationToken);
    }

    private async Task<PilotProfileDto> ProfileAsync(Pirep pirep, IReadOnlyDictionary<int, string> names, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var theirs = await CrudSource.BackOffice<Pirep>(database).AsNoTracking()
            .Where(report => report.TourId == pirep.TourId && report.Vid == pirep.Vid && report.Status != PirepStatus.Withdrawn)
            .Select(report => new { report.Status, report.IsDisputed })
            .ToListAsync(cancellationToken);
        var bans = await CrudSource.BackOffice<Ban>(database).AsNoTracking()
            .Where(ban => ban.Vid == pirep.Vid)
            .OrderByDescending(ban => ban.StartsAt)
            .ToListAsync(cancellationToken);

        return new PilotProfileDto(
            Member(pirep.Vid, names)!,
            theirs.Count,
            theirs.Count(report => report.Status == PirepStatus.Accepted),
            theirs.Count(report => report.Status == PirepStatus.Rejected),
            theirs.Count(report => report.Status == PirepStatus.Rejected && report.IsDisputed),
            [.. bans.Select(ban => new PilotBanDto(ban.TourId, ban.StartsAt, ban.EndsAt, ban.Reason, ban.StartsAt <= now && (ban.EndsAt is null || ban.EndsAt > now)))]);
    }

    private async Task<string?> ParentSlugAsync(Tour tour, CancellationToken cancellationToken) =>
        tour.ParentTourId is { } parent
            ? await CrudSource.BackOffice<Tour>(database).Where(row => row.Id == parent).Select(row => row.Slug).FirstOrDefaultAsync(cancellationToken)
            : null;

    /// <summary>
    /// The mail of a decision (§3.5), in the pilot's language: the tour, the flight, the outcome, the note and the rules
    /// broken. Who decided is not in it — the decision is the department's.
    /// </summary>
    private async Task TellThePilotAsync(Pirep pirep, CancellationToken cancellationToken)
    {
        var type = pirep.Status switch
        {
            PirepStatus.Accepted => FlightOpsNotifications.PirepAccepted,
            PirepStatus.ToModify => FlightOpsNotifications.PirepToModify,
            _ => FlightOpsNotifications.PirepRejected,
        };

        var options = division.Value;
        var locale = await hub.Users.AsNoTracking()
            .Where(user => user.Vid == pirep.Vid)
            .Select(user => user.Locale)
            .FirstOrDefaultAsync(cancellationToken) ?? options.DefaultLocale;

        var tour = await CrudSource.BackOffice<Tour>(database).AsNoTracking().FirstAsync(row => row.Id == pirep.TourId, cancellationToken);
        var slug = tour.Slug ?? await ParentSlugAsync(tour, cancellationToken);
        var rules = PirepSubmission.ViolatedRules(pirep);

        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tour"] = Text(tour.Title, locale, options.DefaultLocale),
            ["route"] = $"{pirep.DepartureIcao} → {pirep.ArrivalIcao}",
            ["date"] = pirep.TakeoffAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["note"] = pirep.NoteToPilot ?? catalog.Resolve(locale, "mail.flightops.noNote"),
            ["rules"] = rules.Count == 0
                ? catalog.Resolve(locale, "mail.flightops.noRules")
                : string.Join('\n', rules.Select(rule => $"- {rule.Code} {Text(rule.Title, locale, options.DefaultLocale)}")),
            ["url"] = $"https://{options.Domain}/tours/{slug}",
        };

        await notifications.QueueAsync(new NotificationIntent(type, [NotificationRecipient.Member(pirep.Vid)], data), cancellationToken);
    }

    private static string Text(Localized<string> text, string locale, string fallback) =>
        text.Get(locale) ?? text.Get(fallback) ?? string.Empty;

    /// <summary>
    /// The revisions a flight stored, read with the core's reader of the tracker — the same one that read them at the send — and
    /// handed to the page without the payload they came in.
    /// </summary>
    private static List<ReviewPlanDto> Plans(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. IvaoTrackerReader.ReadFlightPlans(document.RootElement).Select(plan => new ReviewPlanDto(
            plan.Revision,
            plan.FiledAt,
            plan.DepartureIcao,
            plan.ArrivalIcao,
            plan.AlternateIcao,
            plan.SecondAlternateIcao,
            plan.AircraftIcao,
            plan.WakeTurbulence,
            plan.Equipment,
            plan.Transponder,
            plan.FlightRules,
            plan.FlightType,
            plan.Level,
            plan.Speed,
            plan.Route,
            plan.Remarks,
            plan.DepartureTime is { } departure ? (int)departure.TotalMinutes : null,
            plan.EstimatedEnroute is { } enroute ? (int)enroute.TotalMinutes : null))];
    }

    private void Step(Pirep pirep, PirepStatus to, DateTime at, string? note) =>
        pirep.Events.Add(new PirepEvent
        {
            PirepId = pirep.Id,
            FromStatus = pirep.Status,
            ToStatus = to,
            ByVid = currentUser.Vid,
            At = at,
            Note = note,
        });

    private static (ReviewResult, IReadOnlyDictionary<string, string[]>) Refuse(string field, string key) =>
        (ReviewResult.Refused, new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [key] });

    private static string? Trimmed(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
