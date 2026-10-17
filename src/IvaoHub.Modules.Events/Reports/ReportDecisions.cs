using System.Data;
using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Reports;

/// <summary>
/// What a validator sends to decide a report (§5.3): accepted or rejected — a decision left out is <c>Submitted</c>, and refused —, and a note,
/// required to reject.
/// </summary>
public sealed record ReportDecision(ReportState State, string? Note);

/// <summary>
/// The validation of a report of support (design M4 §5.3, E14a; note 2026-10-17-il-pirep-di-supporto, readings 7 and 8):
/// <c>EventReports.Edit</c> asked of the one handler on the report, in its event's care as it is now — denied to its member, the super
/// administrator included (<c>DeniedToStakeholder</c>), with no <c>if</c> of the module. ⚠️ The guard of the interceptor would let a member
/// write their own row (the core's rule of a row a member sent, T11): the handler is asked <b>before</b> anything is written.
/// <para>Accepted or rejected — a note to reject —, and changed later by whoever may decide it (choice 1, as the outcomes of the shifts after
/// Carmine's answer on #283), under the lock of the report's row, so that a correction of its member in the same instant passes before or after,
/// never in between. The member is told (<see cref="EventsNotifications.ReportDecided"/>) once the decision is committed. Only the columns of
/// the decision are written: whoever validates touches nothing else.</para>
/// </summary>
public sealed class ReportDecisions(
    EventsDbContext database,
    EventChildren children,
    EventsMail mail,
    IAuthorizationService authorization,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>A decision that is neither accepted nor rejected.</summary>
    public const string StateKey = "events:errors.reportStateNotAllowed";

    /// <summary>A decision that says what the report already is.</summary>
    public const string SameKey = "events:errors.reportAlreadySo";

    /// <summary>Whether a report in this state may become that one: a report waiting to accepted or rejected, a decided one to the other.</summary>
    public static bool Allowed(ReportState from, ReportState to) =>
        to is ReportState.Accepted or ReportState.Rejected && from != to;

    public async Task<(ReportResult Result, Refusals Problems)> DecideAsync(
        long id,
        ReportDecision decision,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var problems = new Refusals();
        var note = string.IsNullOrWhiteSpace(decision.Note) ? null : decision.Note.Trim();
        if (note?.Length > EventReport.MaxNoteLength)
        {
            problems.Add("note", "errors.text.tooLong");
        }

        var now = clock.UtcNow;

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await database.Database
            .SqlQuery<long>($"SELECT id AS `Value` FROM evt_reports WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        var report = await CrudSource.BackOffice<EventReport>(database).FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (report is null || (await children.AdoptAsync(report, cancellationToken)).Event is not { } row)
        {
            return (ReportResult.NotFound, problems);
        }

        if (!(await authorization.AuthorizeAsync(user, report, EventsPermissions.ReportsEdit)).Succeeded)
        {
            return (ReportResult.Forbidden, problems);
        }

        if (decision.State is not (ReportState.Accepted or ReportState.Rejected))
        {
            problems.Add("state", StateKey);
        }
        else if (!Allowed(report.State, decision.State))
        {
            problems.Add("state", SameKey);
        }

        if (decision.State == ReportState.Rejected && note is null)
        {
            problems.Add("note", "errors.required");
        }

        if (!problems.IsEmpty)
        {
            return (ReportResult.Refused, problems);
        }

        report.State = decision.State;
        report.DecidedBy = currentUser.Vid;
        report.DecidedAt = now;
        report.DecisionNote = note;
        report.UpdatedAt = now;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            return (ReportResult.Changed, problems);
        }

        await mail.ReportDecidedAsync(row, report.Vid, report.Kind, report.State, note, cancellationToken);

        return (ReportResult.Done, problems);
    }
}
