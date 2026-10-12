using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Bookings;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>
/// What a member's verb on their availability came to: done, refused field by field, nothing of theirs there, or changed meanwhile from
/// another window of theirs — read it again (a 409, the review of #256, point 4).
/// </summary>
public enum AvailabilityResult
{
    Done,
    Refused,
    NotFound,
    Changed,
}

/// <summary>What a member sends to give their availability for an event, or to change it (design M4 §4.2): the windows, and a note.</summary>
public sealed record AvailabilityRequest(IReadOnlyList<AtcWindowDto>? Windows, string? Note);

/// <summary>
/// An availability as its controller reads it, on the event's page: the event, the windows in their order of time and the note — to draw
/// the form again for a change —, until when it can be changed or withdrawn (the close, §4.2), and whether it still can: changed until the
/// close of an event not cancelled, withdrawn until the close of any.
/// </summary>
public sealed record MyAvailabilityDto(
    long Id,
    long EventId,
    string EventSlug,
    Localized<string> EventTitle,
    IReadOnlyList<AtcWindowDto> Windows,
    string? Note,
    DateTime ClosesAtUtc,
    bool Changeable,
    bool Withdrawable,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// A member's availability for the events with a roster (design M4 §4.2, E11a): giving it with their windows and a note, changing it,
/// withdrawing it, reading their own — on the event's page. Any signed in member whose ATC rating is at least the setting's least, on an
/// event they see, with a roster and a position, until the close; nobody else's availability, ever: the staff read them per position
/// (<see cref="StaffAvailability"/>).
/// <para>One availability per member per event is the unique index of the database: two requests of the same member in the same instant
/// meet there, and the second is told to try again. Its windows are rows of the member too, replaced whole at every change, in the same
/// unit of work, so the audit keeps every window that went and came.</para>
/// </summary>
public sealed class MemberAvailability(
    EventsDbContext database,
    EventsPeople people,
    ModuleSettingsStore settingsStore,
    RatingVocabulary vocabulary,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>
    /// Gives the member's availability for an event, or changes it (§4.2): an event with a roster they see, not cancelled, with a position,
    /// before the close; their ATC rating at least the setting's least; the windows checked by <see cref="AtcRules.Check"/>.
    /// </summary>
    public async Task<(AvailabilityResult Result, MyAvailabilityDto? Availability, bool Created, Refusals Problems)> SaveAsync(
        long eventId,
        AvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problems = new Refusals();
        var now = clock.UtcNow;
        var row = await database.Events.AsNoTracking().Where(EventState.Seen(now)).FirstOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);
        if (row is null || !row.HasRoster)
        {
            return (AvailabilityResult.NotFound, null, false, problems);
        }

        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
        if (!AtcRules.IsOpen(row, settings, await HasPositionsAsync(row.Id, cancellationToken), now))
        {
            return (AvailabilityResult.Refused, null, false, problems.Add("id", AtcRules.ClosedKey));
        }

        var vid = currentUser.Vid;
        if (!AtcRules.IsRatedEnough(vocabulary, settings, await people.AtcRatingAsync(vid, cancellationToken)))
        {
            return (AvailabilityResult.Refused, null, false, problems.Add("id", AtcRules.RatingTooLowKey));
        }

        var windows = AtcRules.Check(row, request.Windows, problems);
        if (request.Note?.Trim().Length > EventAtcAvailability.MaxNoteLength)
        {
            problems.Add("note", "errors.text.tooLong");
        }

        if (!problems.IsEmpty)
        {
            return (AvailabilityResult.Refused, null, false, problems);
        }

        var availability = await database.AtcAvailabilities.FirstOrDefaultAsync(
            candidate => candidate.EventId == row.Id && candidate.ControllerVid == vid,
            cancellationToken);
        var created = availability is null;

        if (availability is null)
        {
            // In the event's care: the departments and the scope of every row of the event (§1.1).
            availability = new EventAtcAvailability
            {
                EventId = row.Id,
                ControllerVid = vid,
                CreatedAt = now,
                OwnerDepartment = row.OwnerDepartment,
                OwnerDepartmentMask = row.OwnerDepartmentMask,
            };
            database.AtcAvailabilities.Add(availability);
        }
        else
        {
            database.AtcWindows.RemoveRange(await database.AtcWindows.Where(window => window.AvailabilityId == availability.Id).ToListAsync(cancellationToken));
        }

        availability.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        availability.UpdatedAt = now;

        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            await database.SaveChangesAsync(cancellationToken);

            database.AtcWindows.AddRange(windows.Select(window => new EventAtcWindow
            {
                AvailabilityId = availability.Id,
                EventId = row.Id,
                ControllerVid = vid,
                FromUtc = window.FromUtc,
                ToUtc = window.ToUtc,
                OwnerDepartment = availability.OwnerDepartment,
                OwnerDepartmentMask = availability.OwnerDepartmentMask,
            }));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.Duplicated(exception))
        {
            // The same member gave it from another window an instant before: theirs is there, and this is a change of it.
            database.ChangeTracker.Clear();
            return (AvailabilityResult.Refused, null, false, problems.Add("id", BookingRules.TryAgainKey));
        }
        catch (DbUpdateConcurrencyException)
        {
            // Changed or withdrawn from another window of theirs while this change was on its way: what they see is no longer what is
            // stored, and they read it again (409), never «not found» for an availability that may well be there.
            database.ChangeTracker.Clear();
            return (AvailabilityResult.Changed, null, false, problems);
        }

        return (AvailabilityResult.Done, Mine(availability, row, windows, settings, now), created, problems);
    }

    /// <summary>
    /// Withdraws the member's availability (§4.2): until the close — a cancelled event's too. The row and its windows are deleted, as the
    /// core lets the member who sent them do (<c>[WithdrawnByStakeholder]</c>, E10h), loaded first so that the guard reads whose they are;
    /// the audit keeps who withdrew. Somebody else's is not found.
    /// </summary>
    public async Task<(AvailabilityResult Result, Refusals Problems)> WithdrawAsync(long eventId, CancellationToken cancellationToken)
    {
        var problems = new Refusals();
        var vid = currentUser.Vid;
        var availability = await database.AtcAvailabilities.FirstOrDefaultAsync(
            candidate => candidate.EventId == eventId && candidate.ControllerVid == vid,
            cancellationToken);
        if (availability is null)
        {
            return (AvailabilityResult.NotFound, problems);
        }

        // Their own row: the event read past who may see it now (the review of #256, point 3), so an event the member no longer sees
        // still lets them take back what they gave, until the close.
        var row = await CrudSource.BackOffice<Event>(database).AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken);
        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
        if (row is null || !AtcRules.IsWithdrawable(row, settings, clock.UtcNow))
        {
            return (AvailabilityResult.Refused, problems.Add("id", AtcRules.ClosedKey));
        }

        database.AtcWindows.RemoveRange(await database.AtcWindows.Where(window => window.AvailabilityId == availability.Id).ToListAsync(cancellationToken));
        database.AtcAvailabilities.Remove(availability);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Changed or withdrawn from another window of theirs a moment before: they read it again.
            return (AvailabilityResult.Changed, problems);
        }

        return (AvailabilityResult.Done, problems);
    }

    /// <summary>The member's own availabilities, past ones too, by the start of their events. A visitor has none.</summary>
    public async Task<IReadOnlyList<MyAvailabilityDto>> MineAsync(CancellationToken cancellationToken)
    {
        var vid = currentUser.Vid;
        var now = clock.UtcNow;

        var rows = await database.AtcAvailabilities.AsNoTracking()
            .Where(availability => availability.ControllerVid == vid)
            // Their own rows, every one: the events read past who may see them now (the review of #256, point 3).
            .Join(CrudSource.BackOffice<Event>(database).AsNoTracking(), availability => availability.EventId, row => row.Id, (availability, row) => new { availability, row })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(entry => entry.availability.Id).ToList();
        var windows = (await database.AtcWindows.AsNoTracking()
                .Where(window => ids.Contains(window.AvailabilityId))
                .OrderBy(window => window.FromUtc)
                .ToListAsync(cancellationToken))
            .ToLookup(window => window.AvailabilityId, window => new AtcWindowDto(window.FromUtc, window.ToUtc));
        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);

        return
        [
            .. rows
                .OrderBy(entry => entry.row.StartsAtUtc)
                .ThenBy(entry => entry.availability.Id)
                .Select(entry => Mine(entry.availability, entry.row, [.. windows[entry.availability.Id]], settings, now)),
        ];
    }

    /// <summary>Whether the event opens at least one position: there is nothing to be available for without one.</summary>
    public Task<bool> HasPositionsAsync(long eventId, CancellationToken cancellationToken) =>
        database.AtcPositions.AnyAsync(position => position.EventId == eventId, cancellationToken);

    private static MyAvailabilityDto Mine(
        EventAtcAvailability availability,
        Event row,
        IReadOnlyList<AtcWindowDto> windows,
        EventsSettings settings,
        DateTime now) =>
        new(
            availability.Id,
            row.Id,
            row.Slug,
            row.Title,
            windows,
            availability.Note,
            AtcRules.ClosesAt(row, settings),
            AtcRules.IsWithdrawable(row, settings, now) && row.CancelledAt is null,
            AtcRules.IsWithdrawable(row, settings, now),
            availability.CreatedAt,
            availability.UpdatedAt);
}
