using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.People;
using IvaoHub.Modules.Events.Atc;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Public;

/// <summary>
/// One row of a published roster as the site shows it (§4.4, §7.1, E12): the position — callsign and the name the core knows —, the hours,
/// whether somebody holds them, and who: named, with their ATC rating as the staff say it, <b>only to a signed in reader</b>; a visitor reads
/// «assigned» or «uncovered». Neither the why nor the warnings: they are the staff's.
/// </summary>
public sealed record PublicRosterRowDto(
    long PositionId,
    string Callsign,
    string? Name,
    DateTime FromUtc,
    DateTime ToUtc,
    bool Covered,
    EventMemberDto? Controller,
    string? Rating);

/// <summary>
/// The roster of an event as its page shows it (§4.4, E12): the event's head, when the roster is published, whether it is — before, no row —,
/// and the rows by callsign and hour.
/// </summary>
public sealed record PublicRosterDto(
    long EventId,
    string Slug,
    Localized<string> Title,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime? CancelledAt,
    DateTime PublishesAtUtc,
    bool Published,
    IReadOnlyList<PublicRosterRowDto> Rows);

/// <summary>
/// The page of a roster, <c>/events/{slug}/roster</c> (design M4 §4.4, §7.1, E12; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione,
/// reading 5), and its read, <c>GET /api/events/public/{slug}/roster</c>: ⚠️ <b>the seventh hand written read of the ATC</b>, anonymous as the
/// read of the page of an event is, because an uncovered piece is no row (reading 2 of E11b) and the list of the engine does not give it.
/// <para>For an event with a roster that the reader sees — the same rule as the page (<see cref="PublicEvents.FindAsync"/>) —: before its
/// publication only when it will be; after it, every position's shifts and uncovered pieces, the latter with the staff's own function
/// (<see cref="StaffRoster.Gaps"/> on <see cref="RosterProposer.Pieces(DateTime, DateTime, int)"/>).</para>
/// </summary>
public sealed class PublicRoster(
    EventsDbContext database,
    HubDbContext hub,
    PublicEvents events,
    IAtcPositionDirectory directory,
    ModuleSettingsStore settingsStore,
    RatingVocabulary vocabulary,
    EventsPeople people,
    ICurrentUser currentUser)
{
    public async Task<PublicRosterDto?> ReadAsync(string slug, System.Security.Claims.ClaimsPrincipal reader, CancellationToken cancellationToken)
    {
        var (row, _) = await events.FindAsync(slug, reader, cancellationToken);
        if (row is null || !row.HasRoster)
        {
            return null;
        }

        var settings = await settingsStore.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken);
        var published = row.RosterPublishedAt is not null;
        var rows = published ? await RowsAsync(row, settings, cancellationToken) : [];

        return new PublicRosterDto(
            row.Id,
            row.Slug,
            row.Title,
            row.StartsAtUtc,
            row.EndsAtUtc,
            row.CancelledAt,
            RosterPublication.PublishesAt(row, settings),
            published,
            rows);
    }

    private async Task<IReadOnlyList<PublicRosterRowDto>> RowsAsync(Event row, EventsSettings settings, CancellationToken cancellationToken)
    {
        var positions = await database.AtcPositions.AsNoTracking().Where(position => position.EventId == row.Id).ToListAsync(cancellationToken);
        var shifts = await CrudSource.BackOffice<EventAtcShift>(database).AsNoTracking().Where(shift => shift.EventId == row.Id).ToListAsync(cancellationToken);
        var named = await directory.FindAsync([.. positions.Select(position => position.Callsign).Distinct()], cancellationToken);

        // The people only to a signed in reader (§4.4): a visitor gets no VID, no name, no rating.
        var signedIn = currentUser.IsAuthenticated;
        var names = signedIn ? await MemberNames.ReadAsync(hub, shifts.Select(shift => shift.ControllerVid), cancellationToken) : new Dictionary<int, string>();
        var ratings = signedIn ? await people.AtcRatingsAsync(shifts.Select(shift => shift.ControllerVid), cancellationToken) : new Dictionary<int, int>();
        var minutes = RosterData.Rules(row, settings).ShiftMinutes;

        var rows = new List<PublicRosterRowDto>();
        foreach (var position in positions)
        {
            var name = named.GetValueOrDefault(position.Callsign)?.Name;
            var own = shifts.Where(shift => shift.PositionId == position.Id).ToList();

            rows.AddRange(own.Select(shift => new PublicRosterRowDto(
                position.Id,
                position.Callsign,
                name,
                shift.FromUtc,
                shift.ToUtc,
                Covered: true,
                signedIn ? EventsPeople.Member(shift.ControllerVid, names) : null,
                signedIn && ratings.TryGetValue(shift.ControllerVid, out var rating) ? vocabulary.Find(RatingKind.Atc, rating)?.ShortName : null)));

            foreach (var (fromUtc, toUtc) in RosterProposer.Pieces(position.FromUtc, position.ToUtc, minutes))
            {
                rows.AddRange(StaffRoster.Gaps(fromUtc, toUtc, own).Select(gap => new PublicRosterRowDto(
                    position.Id,
                    position.Callsign,
                    name,
                    gap.FromUtc,
                    gap.ToUtc,
                    Covered: false,
                    Controller: null,
                    Rating: null)));
            }
        }

        return
        [
            .. rows
                .OrderBy(entry => entry.Callsign, StringComparer.Ordinal)
                .ThenBy(entry => entry.PositionId)
                .ThenBy(entry => entry.FromUtc),
        ];
    }
}

/// <summary>The read of the page of a roster (E12): anonymous, answered for whoever asks, 404 for an event the reader does not see or without a roster.</summary>
public static class PublicRosterEndpoints
{
    public static IEndpointRouteBuilder MapPublicRosterEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{PublicEventEndpoints.Pattern}/{{slug}}/roster", async (string slug, PublicRoster roster, HttpContext http) =>
                await roster.ReadAsync(slug, http.User, http.RequestAborted) is { } page ? Results.Ok(page) : Results.NotFound())
            .WithName("EventsPublicRoster")
            .WithTags("Events")
            .Produces<PublicRosterDto>()
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        return app;
    }
}
