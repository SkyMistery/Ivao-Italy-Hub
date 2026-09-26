using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.Threads;

/// <summary>
/// A pilot's dispute of a rejection (design M2 §3.8; note 2026-09-23-contestazioni-chiarimenti-segnalazioni §3.1): opening it —
/// the report flags it and, in the same save, opens the thread with its department (<see cref="Pirep.Project"/>) — and deciding
/// it. One per report: the thread is opened once.
/// <para>Deciding asks for <see cref="TourPermissions.ReopenDecisions"/> on the report, and is never for whoever decided it (Carmine,
/// 23 September 2026): they take part in the thread and answer, they do not judge their own decision. The answer goes into the
/// thread as the department's, which is how the pilot hears the outcome — no mail of its own.</para>
/// </summary>
public sealed class PirepDisputes(
    FlightOpsDbContext database,
    HubDbContext hub,
    FlightOpsReferences references,
    ContactThreads threads,
    IAuthorizationService authorization,
    IHttpContextAccessor http,
    ModuleSettingsStore settingsStore,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>Until when this report may still be disputed now; none when it may not be — not rejected, already disputed, or too late.</summary>
    public static DateTime? DisputableUntil(Pirep pirep, int windowDays, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        return pirep.Status == PirepStatus.Rejected
            && pirep.DisputeStatus is null
            && pirep.DecidedAt is { } decided
            && decided.AddDays(windowDays) is var until
            && now <= until
                ? until
                : null;
    }

    /// <summary>
    /// The threads the disputes of these reports opened, by report, among those the reader may read: the core's filter answers
    /// for the pilot (the sender), the validator (a participant) and the department alike.
    /// </summary>
    public static async Task<IReadOnlyDictionary<long, long>> ThreadsAsync(
        HubDbContext hub,
        IReadOnlyCollection<long> pirepIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(pirepIds);

        if (pirepIds.Count == 0)
        {
            return new Dictionary<long, long>();
        }

        var sources = pirepIds.Select(Pirep.ReferenceOf).ToList();
        var opened = await hub.ContactMessages.AsNoTracking()
            .Where(message => message.SourceModule == FlightOpsModule.ModuleKey
                && message.Kind == ContactKinds.Dispute
                && sources.Contains(message.SourceId!))
            .Select(message => new { message.SourceId, message.Id })
            .ToListAsync(cancellationToken);

        return opened.ToDictionary(message => pirepIds.First(id => Pirep.ReferenceOf(id) == message.SourceId), message => message.Id);
    }

    /// <summary>
    /// The pilot disputes one of their rejections, within <c>disputeWindowDays</c> of the decision: the report is flagged, the leg
    /// no longer holds the next ones, and the thread opens in the same save with whoever decided among its participants.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string[]>?> OpenAsync(
        Pirep pirep,
        PilotTour pilot,
        DisputeOpenDto body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pirep);
        ArgumentNullException.ThrowIfNull(pilot);
        ArgumentNullException.ThrowIfNull(body);

        var now = clock.UtcNow;
        var settings = await settingsStore.GetAsync<FlightOpsSettings>(FlightOpsModule.ModuleKey, cancellationToken);
        if (DisputableUntil(pirep, settings.DisputeWindowDays, now) is null)
        {
            return Refusal("status", pirep switch
            {
                { DisputeStatus: not null } => "flightops:errors.disputeAlready",
                { Status: not PirepStatus.Rejected } => "flightops:errors.disputeNotRejected",
                _ => "flightops:errors.disputeWindowClosed",
            });
        }

        var text = Trimmed(body.Text);
        if (text is null || text.Length > PirepValidation.MaxTextLength)
        {
            return Refusal("text", text is null ? "errors.required" : "errors.text.tooLong");
        }

        var label = references.PirepLabel(pilot.Tour, pirep);
        var locale = currentUser.Locale;

        database.Entry(pirep).Property(row => row.RowVersion).OriginalValue = body.RowVersion;
        database.PirepEvents.Add(new PirepEvent
        {
            PirepId = pirep.Id,
            FromStatus = pirep.Status,
            ToStatus = pirep.Status,
            ByVid = currentUser.Vid,
            At = now,
            Note = "flightops:events.disputed",
        });

        pirep.DisputeStatus = DisputeStatus.Open;
        pirep.DisputeText = text;
        pirep.DisputedAt = now;
        pirep.DisputeThread = new ThreadOpeningProjection(
            ContactKinds.Dispute,
            pirep.OwnerDepartment,
            Clip(references.Words(locale, "flightops:threads.disputeSubject", ("label", label.Get(locale) ?? label.Values.FirstOrDefault() ?? string.Empty))),
            text,
            pirep.Vid,
            pirep.DecidedByVid is { } decider ? [decider] : [],
            [new ThreadReferenceProjection(FlightOpsModule.ModuleKey, pirep.SourceId, label)]);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            pirep.DisputeThread = null;
        }

        // The mails, after the save that opened the thread (T14a): the department hears it arrived, the validator that they
        // take part.
        if ((await ThreadsAsync(hub, [pirep.Id], cancellationToken)).TryGetValue(pirep.Id, out var threadId))
        {
            await threads.NotifyOpenedAsync(threadId, cancellationToken);
        }

        return null;
    }

    /// <summary>Whether the reader may uphold or turn down this report's dispute: <c>Tours.ReopenDecisions</c>, and not whoever decided it.</summary>
    public async Task<bool> MayDecideAsync(Pirep pirep)
    {
        ArgumentNullException.ThrowIfNull(pirep);

        return pirep.DecidedByVid != currentUser.Vid
            && http.HttpContext is { } context
            && (await authorization.AuthorizeAsync(context.User, pirep, TourPermissions.ReopenDecisions)).Succeeded;
    }

    /// <summary>
    /// Upholds a dispute — the report goes back to the queue, to anybody, for a new decision — or turns it down — the rejection
    /// holds the next legs again, the grace counted from now —, and puts the answer in the thread, which mails the pilot.
    /// </summary>
    public async Task<(ReviewResult Result, IReadOnlyDictionary<string, string[]>? Problems)> DecideAsync(
        Pirep pirep,
        DisputeDecisionDto decision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pirep);
        ArgumentNullException.ThrowIfNull(decision);

        if (!await MayDecideAsync(pirep))
        {
            return (ReviewResult.Forbidden, null);
        }

        if (pirep.Status != PirepStatus.Rejected || pirep.DisputeStatus != DisputeStatus.Open)
        {
            return (ReviewResult.Refused, Refusal("status", "flightops:errors.disputeNotOpen"));
        }

        var answer = Trimmed(decision.Answer);
        if (answer is null || answer.Length > PirepValidation.MaxTextLength)
        {
            return (ReviewResult.Refused, Refusal("answer", answer is null ? "errors.required" : "errors.text.tooLong"));
        }

        // The answer is how the pilot hears it: a decision whose answer could not reach the thread is refused, not taken mute.
        if (!(await ThreadsAsync(hub, [pirep.Id], cancellationToken)).TryGetValue(pirep.Id, out var threadId))
        {
            return (ReviewResult.Refused, Refusal("answer", "flightops:errors.disputeThreadUnreadable"));
        }

        var now = clock.UtcNow;
        database.Entry(pirep).Property(row => row.RowVersion).OriginalValue = decision.RowVersion;

        var to = decision.Upheld ? PirepStatus.Queued : PirepStatus.Rejected;
        database.PirepEvents.Add(new PirepEvent
        {
            PirepId = pirep.Id,
            FromStatus = pirep.Status,
            ToStatus = to,
            ByVid = currentUser.Vid,
            At = now,
            Note = decision.Upheld ? "flightops:events.disputeUpheld" : "flightops:events.disputeDismissed",
        });

        pirep.DisputeStatus = decision.Upheld ? DisputeStatus.Upheld : DisputeStatus.Dismissed;
        pirep.DisputeDecidedAt = now;
        pirep.DisputeDecidedByVid = currentUser.Vid;

        if (decision.Upheld)
        {
            // Back in the queue as after a correction (§3.1): the old decision's errors stay until the next one replaces them,
            // and meanwhile count for nothing — only accepted and rejected reports do.
            pirep.Status = PirepStatus.Queued;
            pirep.QueuedAt = now;
            pirep.AssignedToVid = null;
            pirep.LeaseUntil = null;
        }

        await database.SaveChangesAsync(cancellationToken);

        // Two saves in two contexts, like the mails after a decision: should this one fail, the decision stands and the answer
        // is written in the thread by hand.
        await threads.ReplyAsync(http.HttpContext!.User, threadId, answer, cancellationToken);

        return (ReviewResult.Done, null);
    }

    /// <summary>A subject fits its column; the label of a long tour title may not.</summary>
    private static string Clip(string subject) =>
        subject.Length <= ContactSubmitDtoValidator.MaxSubjectLength ? subject : subject[..ContactSubmitDtoValidator.MaxSubjectLength];

    private static Dictionary<string, string[]> Refusal(string field, string key) =>
        new(StringComparer.Ordinal) { [field] = [key] };

    private static string? Trimmed(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
