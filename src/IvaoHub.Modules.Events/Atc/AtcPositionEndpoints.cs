using System.Globalization;
using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using IvaoHub.Modules.Events.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>
/// A position of an event as its list and its form show it (design M4 §1.7): the callsign, the name the core knows and its FIR, the hours,
/// the staff's note, and how many controllers are available in some of its hours — read with a page of positions; none for a row read alone.
/// </summary>
public sealed record EventAtcPositionDto(
    long Id,
    long EventId,
    Department OwnerDepartment,
    string Callsign,
    string? Name,
    string? Fir,
    DateTime FromUtc,
    DateTime ToUtc,
    string? Note,
    int Candidates,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// What a client may set on a position of an event. The event is chosen when it is created and never changes; the care is the event's
/// and the FIR the core's directory's, both taken before the permission is asked. No hours is the event's window.
/// </summary>
public sealed record EventAtcPositionWriteDto(
    long EventId,
    string Callsign,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Note,
    DateTime RowVersion);

/// <summary>
/// A controller available in some of the hours of a position (design M4 §4.2, §6.3, E11a), as the staff of the ATC read them on the
/// position: the controller named the way the core names a person, their ATC rating as the staff say it (the vocabulary's short name), their
/// windows in UTC, whether they cover the position from its opening to its closing, their note, and when they gave it — the first first.
/// </summary>
public sealed record AtcCandidateDto(
    long Id,
    EventMemberDto Controller,
    string? Rating,
    bool WholePosition,
    IReadOnlyList<string> Windows,
    string? Note,
    DateTime CreatedAt);

/// <summary>
/// The head of an event as the staff of the ATC read it (design M4 §6.3, E2's form, E11a): what the page of its ATC shows and needs —
/// its words, its state, its window, when its availabilities close and how long a shift is — to whoever holds <c>EventAtc.View</c>
/// somewhere, the staff of a FIR among them, who do not read the event itself.
/// </summary>
public sealed record AtcEventDto(
    long Id,
    Department OwnerDepartment,
    string Slug,
    Localized<string> Title,
    EventStateKind State,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime ClosesAtUtc,
    int ShiftMinutes,
    DateTime? CancelledAt);

/// <summary>A position of the division as the form of a position offers it: the callsign, its name, its kind and its FIR.</summary>
public sealed record AtcPositionChoiceDto(string Callsign, string Name, string Type, string? Fir);

/// <summary>An ATC rating of the core's vocabulary as the settings offer it: the network's number, and the short name the staff say.</summary>
public sealed record AtcRatingChoiceDto(int Number, string ShortName);

/// <summary>The rules one payload can answer by itself; whether the callsign is a position of the division and fits the event is the save's.</summary>
public sealed class EventAtcPositionWriteDtoValidator : AbstractValidator<EventAtcPositionWriteDto>
{
    public EventAtcPositionWriteDtoValidator()
    {
        RuleFor(position => position.EventId).GreaterThan(0).WithMessage("errors.required");
        RuleFor(position => position.Callsign)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(EventAtcPosition.MaxCallsignLength).WithMessage("errors.text.tooLong");
        RuleFor(position => position.ToUtc)
            .GreaterThan(position => position.FromUtc)
            .When(position => position.FromUtc is not null && position.ToUtc is not null)
            .WithMessage(AtcRules.EndsBeforeItStartsKey);
        RuleFor(position => position.Note).MaximumLength(EventAtcPosition.MaxNoteLength).WithMessage("errors.text.tooLong");
    }
}

/// <summary>Positions to and from their payloads: by hand, the callsign in upper case as the core writes it.</summary>
internal static class AtcPositionMapper
{
    public static EventAtcPositionDto ToDto(EventAtcPosition position) => Row(position, name: null, candidates: 0);

    public static EventAtcPositionDto Row(EventAtcPosition position, string? name, int candidates) => new(
        position.Id,
        position.EventId,
        position.OwnerDepartment,
        position.Callsign,
        name,
        position.Fir,
        position.FromUtc,
        position.ToUtc,
        position.Note,
        candidates,
        position.UpdatedAt,
        position.RowVersion);

    /// <summary>
    /// The event only on a new row: a position never moves to another event. No hours is none here, and the event's window before the
    /// permission is asked (<see cref="AtcPositionEndpoints"/>); an empty note is none.
    /// </summary>
    public static void Apply(EventAtcPositionWriteDto payload, EventAtcPosition position)
    {
        if (position.Id == 0)
        {
            position.EventId = payload.EventId;
        }

        position.Callsign = payload.Callsign.Trim().ToUpperInvariant();
        position.FromUtc = payload.FromUtc ?? default;
        position.ToUtc = payload.ToUtc ?? default;
        position.Note = string.IsNullOrWhiteSpace(payload.Note) ? null : payload.Note.Trim();
        position.RowVersion = payload.RowVersion;
    }
}

/// <summary>
/// The ATC of an event in the staff's back office (design M4 §4.1, §6.3, §7.2, E11a; note 2026-10-12-le-postazioni-e-la-disponibilita).
/// <list type="bullet">
/// <item><b>The positions</b>: a resource of the CRUD engine filtered by <c>filter[eventId]</c>, read with <c>EventAtc.View</c> and written
/// with <c>EventAtc.Edit</c> on the event's care and on the position's FIR — the area of the ATC operations and of the staff of the FIRs
/// (§6.2), who write the positions of an event and not the event. A position is a callsign of the core's positions of the division, whose
/// FIR the server copies from the core's directory before the permission is asked: with <c>firStaffScope: own</c> the staff of a FIR
/// write and list the positions of their FIR alone, the one handler, the guard and the list saying the same.</item>
/// <item>⚠️ <b>Four hand written reads</b> (readings 1 and 2 of the note): the controllers available on one position, asked of the one
/// handler on the position — so the staff of a FIR read the candidates of their positions, which say no FIR of their own (E2's form,
/// point 7) —; the heads of the events with a roster, for whoever holds <c>EventAtc.View</c> somewhere and not <c>Events.View</c>
/// (E2's form, point 7); and the two lists of the core the forms choose from, the positions of the division and the ATC ratings, as the
/// training reads its own (<c>/api/training/ratings</c>, <c>/positions</c>).</item>
/// </list>
/// </summary>
public static class AtcPositionEndpoints
{
    public const string Pattern = "/api/events/atc/positions";

    /// <summary>The heads of the events with a roster, for the staff of the ATC.</summary>
    public const string EventsPattern = "/api/events/atc/events";

    /// <summary>The positions of the division, which a position of an event is chosen from.</summary>
    public const string DivisionPositionsPattern = "/api/events/atc/division-positions";

    /// <summary>The ATC ratings of the core's vocabulary, which the least rating of the settings is chosen from.</summary>
    public const string RatingsPattern = "/api/events/atc/ratings";

    private const string UnknownPositionKey = "events:errors.positionUnknown";

    public static IEndpointRouteBuilder MapAtcPositionEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapCrud<EventAtcPosition, EventAtcPositionDto, EventAtcPositionDto, EventAtcPositionWriteDto>(Pattern, options =>
        {
            options.PermissionArea = EventsPermissions.AtcArea;
            options.Name = "EventAtcPositions";
            options.ReadPolicy = EventsPermissions.AtcView;
            options.WritePolicy = EventsPermissions.AtcEdit;
            options.ContextType = typeof(EventsDbContext);

            // By the hour they open, as the roster reads them; the callsign sorts by hand.
            options.DefaultOrder = position => position.FromUtc;
            options.Sortable.Add(nameof(EventAtcPosition.Callsign));
            options.Sortable.Add(nameof(EventAtcPosition.FromUtc));
            options.Filterable.Add(nameof(EventAtcPosition.EventId));
            options.SearchFields.Add(position => position.Callsign);

            options.ToList = AtcPositionMapper.ToDto;
            options.ToListPage = (positions, services, cancellationToken) =>
                services.GetRequiredService<StaffAvailability>().PositionsAsync(positions, cancellationToken);
            options.ToDetail = AtcPositionMapper.ToDto;
            options.Apply = AtcPositionMapper.Apply;
            options.BeforeAuthorize = AdoptAsync;
            options.BeforeSave = SaveAsync;
        });

        var atc = app.MapGroup("/api/events/atc").WithTags("EventAtc");

        atc.MapGet("/positions/{id:long}/candidates", CandidatesAsync)
            .WithName("EventsAtcCandidates")
            .Produces<PagedResult<AtcCandidateDto>>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(EventsPermissions.AtcView);

        atc.MapGet("/events", EventsAsync)
            .WithName("EventsAtcEvents")
            .Produces<PagedResult<AtcEventDto>>()
            .RequireAuthorization(EventsPermissions.AtcView);

        atc.MapGet("/events/{id:long}", EventAsync)
            .WithName("EventsAtcEvent")
            .Produces<AtcEventDto>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(EventsPermissions.AtcView);

        atc.MapGet("/division-positions", async (IAtcPositionDirectory directory, HttpContext http) =>
                (await directory.OfDivisionAsync(http.RequestAborted))
                    .Select(position => new AtcPositionChoiceDto(position.Callsign, position.Name, position.Type, position.Fir))
                    .ToList())
            .WithName("EventsAtcDivisionPositions")
            .Produces<List<AtcPositionChoiceDto>>()
            .RequireAuthorization(EventsPermissions.AtcEdit);

        atc.MapGet("/ratings", (RatingVocabulary vocabulary) =>
                vocabulary.Ladder(RatingKind.Atc).Select(rating => new AtcRatingChoiceDto(rating.Number, rating.ShortName)).ToList())
            .WithName("EventsAtcRatings")
            .Produces<List<AtcRatingChoiceDto>>()
            .RequireAuthorization(EventsPermissions.ManageSettings);

        return app;
    }

    /// <summary>
    /// What a position takes from other rows before its permission is asked: the event's care, the event's window for hours left empty,
    /// and the FIR of its callsign from the core's directory — the one handler and the guard ask the permission on that FIR, so it is
    /// never the client's to say. A callsign that is not a position of the division is refused here, on its field.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> AdoptAsync(EventAtcPosition position, CrudSaving saving)
    {
        var (parent, refusal) = await saving.Services.GetRequiredService<EventChildren>().AdoptAsync(position, saving.CancellationToken);
        if (parent is null)
        {
            return refusal;
        }

        if (position.FromUtc == default)
        {
            position.FromUtc = parent.StartsAtUtc;
        }

        if (position.ToUtc == default)
        {
            position.ToUtc = parent.EndsAtUtc;
        }

        var found = await saving.Services.GetRequiredService<IAtcPositionDirectory>()
            .FindAsync([position.Callsign], saving.CancellationToken);
        if (found.TryGetValue(position.Callsign, out var known))
        {
            position.Callsign = known.Callsign;
            position.Fir = known.Fir;
            return null;
        }

        // One the directory no longer lists keeps the FIR it was written with, so that it can still be corrected or deleted; a new
        // callsign has to be one the directory lists.
        return !saving.IsNew
            && string.Equals(
                (string?)saving.Database.Entry(position).Property(nameof(EventAtcPosition.Callsign)).OriginalValue,
                position.Callsign,
                StringComparison.Ordinal)
            ? null
            : EventChildren.Refusal("callsign", UnknownPositionKey);
    }

    /// <summary>
    /// A position belongs to an event with a roster (reading 3), inside its window, and opens once at a time: the same callsign twice in an
    /// event only in hours that do not meet (reading 4).
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> SaveAsync(EventAtcPosition position, CrudSaving saving)
    {
        if (await saving.Services.GetRequiredService<EventChildren>().EventAsync(position.EventId, saving.CancellationToken) is not { } parent)
        {
            return EventChildren.Refusal("eventId", "events:errors.eventUnknown");
        }

        if (!parent.HasRoster)
        {
            return EventChildren.Refusal("eventId", "events:errors.eventHasNoRoster");
        }

        if (position.ToUtc <= position.FromUtc)
        {
            return EventChildren.Refusal("toUtc", AtcRules.EndsBeforeItStartsKey);
        }

        if (!AtcRules.IsInside(parent, position.FromUtc, position.ToUtc))
        {
            return EventChildren.Refusal("fromUtc", "events:errors.positionOutsideEvent");
        }

        var database = (EventsDbContext)saving.Database;
        var twice = await database.AtcPositions.AnyAsync(
            other => other.EventId == position.EventId
                && other.Callsign == position.Callsign
                && other.Id != position.Id
                && other.FromUtc < position.ToUtc
                && position.FromUtc < other.ToUtc,
            saving.CancellationToken);

        return twice ? EventChildren.Refusal("callsign", "events:errors.positionTwice") : null;
    }

    /// <summary>
    /// The controllers available in some of the hours of a position (design §6.3): asked of the one handler on the position, with its FIR,
    /// so whoever reads it reads its candidates; a page of them, the first given first.
    /// </summary>
    private static async Task<IResult> CandidatesAsync(
        long id,
        int? page,
        int? pageSize,
        string? q,
        EventsDbContext database,
        StaffAvailability availability,
        IAuthorizationService authorization,
        ICurrentUser currentUser,
        LocaleCatalog catalog,
        HttpContext http)
    {
        var position = await CrudSource.BackOffice<EventAtcPosition>(database).AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, http.RequestAborted);
        if (position is null)
        {
            return Problem(StatusCodes.Status404NotFound, CrudProblems.NotFoundTitleKey, catalog, currentUser);
        }

        if (!(await authorization.AuthorizeAsync(http.User, position, EventsPermissions.AtcView)).Succeeded)
        {
            return Problem(StatusCodes.Status403Forbidden, CrudProblems.ForbiddenTitleKey, catalog, currentUser);
        }

        // The list's search box: a VID, or a piece of a name.
        var all = (await availability.CandidatesAsync(position, http.RequestAborted))
            .Where(candidate => string.IsNullOrWhiteSpace(q)
                || candidate.Controller.Vid.ToString(CultureInfo.InvariantCulture).Contains(q.Trim(), StringComparison.Ordinal)
                || candidate.Controller.Name?.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        var size = Math.Clamp(pageSize ?? 20, 1, 100);
        var number = Math.Max(page ?? 1, 1);

        return Results.Ok(new PagedResult<AtcCandidateDto>([.. all.Skip((number - 1) * size).Take(size)], number, size, all.Count));
    }

    /// <summary>The events with a roster still to end, the soonest first — drafts too: the positions are opened before an event is published.</summary>
    private static async Task<PagedResult<AtcEventDto>> EventsAsync(
        int? page,
        int? pageSize,
        string? q,
        EventsDbContext database,
        ModuleSettingsStore settings,
        IClock clock,
        HttpContext http)
    {
        var now = clock.UtcNow;
        var size = Math.Clamp(pageSize ?? 20, 1, 100);
        var number = Math.Max(page ?? 1, 1);
        var query = CrudSource.BackOffice<Event>(database).AsNoTracking().Where(row => row.HasRoster && row.EndsAtUtc > now);

        // The list's search box: a piece of the event's address, which says its name in the division's words.
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(row => row.Slug.Contains(term));
        }

        var total = await query.CountAsync(http.RequestAborted);
        var rows = await query
            .OrderBy(row => row.StartsAtUtc)
            .ThenBy(row => row.Id)
            .Skip((number - 1) * size)
            .Take(size)
            .ToListAsync(http.RequestAborted);
        var current = await settings.GetAsync<EventsSettings>(EventsModule.ModuleKey, http.RequestAborted);

        return new PagedResult<AtcEventDto>([.. rows.Select(row => Head(row, current, now))], number, size, total);
    }

    /// <summary>The head of one event with a roster, in any state; an event without one has no ATC to show.</summary>
    private static async Task<IResult> EventAsync(
        long id,
        EventsDbContext database,
        ModuleSettingsStore settings,
        IClock clock,
        HttpContext http)
    {
        var row = await CrudSource.BackOffice<Event>(database).AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.HasRoster, http.RequestAborted);

        return row is null
            ? Results.NotFound()
            : Results.Ok(Head(row, await settings.GetAsync<EventsSettings>(EventsModule.ModuleKey, http.RequestAborted), clock.UtcNow));
    }

    private static AtcEventDto Head(Event row, EventsSettings settings, DateTime now) => new(
        row.Id,
        row.OwnerDepartment,
        row.Slug,
        row.Title,
        EventState.Of(row, now),
        row.StartsAtUtc,
        row.EndsAtUtc,
        AtcRules.ClosesAt(row, settings),
        row.ShiftMinutes ?? settings.ShiftMinutes,
        row.CancelledAt);

    private static IResult Problem(int status, string titleKey, LocaleCatalog catalog, ICurrentUser currentUser) =>
        Results.Problem(statusCode: status, title: catalog.Resolve(currentUser.Locale, titleKey));

    /// <summary>A window as the staff read it in a list: in UTC, the day once when it ends on the day it starts.</summary>
    public static string Written(DateTime fromUtc, DateTime toUtc) =>
        fromUtc.Date == toUtc.Date
            ? string.Create(CultureInfo.InvariantCulture, $"{fromUtc:yyyy-MM-dd HH:mm}–{toUtc:HH:mm} UTC")
            : string.Create(CultureInfo.InvariantCulture, $"{fromUtc:yyyy-MM-dd HH:mm} – {toUtc:yyyy-MM-dd HH:mm} UTC");
}
