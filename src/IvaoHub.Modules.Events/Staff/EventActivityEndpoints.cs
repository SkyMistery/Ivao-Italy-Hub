using FluentValidation;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registrations;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>
/// A parallel activity as its list and its form show it (design M4 §4-bis.1): its words, its window, the length of a turn, the seats of
/// each, how many turns it has, and <c>SeatsTaken</c> — how many seats the members hold in all its turns, read in the list, the numbers of
/// the statistics once the event is past its retention (<see cref="InPersonRetention"/>); none on a row read alone.
/// </summary>
public sealed record EventActivityDto(
    long Id,
    long EventId,
    Department OwnerDepartment,
    Localized<string> Name,
    Localized<string>? Place,
    DateTime FromUtc,
    DateTime ToUtc,
    int SlotMinutes,
    int SeatsPerSlot,
    int Turns,
    int SeatsTaken,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// What a client may set on an activity. The event is chosen when it is created and never changes; the care is the event's, taken before
/// the permission is asked. An empty place is none: the event's venue.
/// </summary>
public sealed record EventActivityWriteDto(
    long EventId,
    Localized<string> Name,
    Localized<string>? Place,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int SlotMinutes,
    int SeatsPerSlot,
    DateTime RowVersion);

/// <summary>
/// The rules one payload can answer by itself; whether its event is in person, whether its window fits the event, and what the seats taken
/// forbid, are the save's. The members read the name and the place, so they are written in every language of the division. Messages are
/// i18n keys.
/// </summary>
public sealed class EventActivityWriteDtoValidator : AbstractValidator<EventActivityWriteDto>
{
    public EventActivityWriteDtoValidator(IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(division);

        RuleFor(activity => activity.EventId).GreaterThan(0).WithMessage("errors.required");
        RuleFor(activity => activity.Name).Required(division.Value);
        RuleFor(activity => activity.Name)
            .Must(name => name is null || name.Values.All(text => (text?.Length ?? 0) <= EventActivity.MaxTextLength))
            .WithMessage("errors.text.tooLong");
        RuleFor(activity => activity.Place!)
            .Required(division.Value)
            .When(activity => EventMapper.IsWritten(activity.Place));
        RuleFor(activity => activity.Place)
            .Must(place => place is null || place.Values.All(text => (text?.Length ?? 0) <= EventActivity.MaxTextLength))
            .WithMessage("errors.text.tooLong");
        RuleFor(activity => activity.FromUtc).NotNull().WithMessage("errors.required");
        RuleFor(activity => activity.ToUtc).NotNull().WithMessage("errors.required");
        RuleFor(activity => activity.ToUtc)
            .GreaterThan(activity => activity.FromUtc)
            .When(activity => activity.FromUtc is not null && activity.ToUtc is not null)
            .WithMessage("events:errors.endsBeforeItStarts");
        RuleFor(activity => activity.SlotMinutes)
            .InclusiveBetween(EventActivity.MinSlotMinutes, EventActivity.MaxSlotMinutes)
            .WithMessage("errors.number.range");
        RuleFor(activity => activity.SeatsPerSlot)
            .InclusiveBetween(1, EventActivity.MaxSeatsPerSlot)
            .WithMessage("errors.number.range");
    }
}

/// <summary>Activities to and from their payloads.</summary>
internal static class ActivityMapper
{
    public static EventActivityDto ToDto(EventActivity activity) => Row(activity, seatsTaken: 0);

    public static EventActivityDto Row(EventActivity activity, int seatsTaken) => new(
        activity.Id,
        activity.EventId,
        activity.OwnerDepartment,
        activity.Name,
        activity.Place,
        activity.FromUtc,
        activity.ToUtc,
        activity.SlotMinutes,
        activity.SeatsPerSlot,
        Math.Min(activity.Turns().Count, EventActivity.MaxTurns),
        seatsTaken,
        activity.UpdatedAt,
        activity.RowVersion);

    /// <summary>The event only on a new row: an activity never moves to another event. An empty place is none.</summary>
    public static void Apply(EventActivityWriteDto payload, EventActivity activity)
    {
        if (activity.Id == 0)
        {
            activity.EventId = payload.EventId;
        }

        activity.Name = payload.Name;
        activity.Place = EventMapper.IsWritten(payload.Place) ? payload.Place : null;
        activity.FromUtc = payload.FromUtc.GetValueOrDefault();
        activity.ToUtc = payload.ToUtc.GetValueOrDefault();
        activity.SlotMinutes = payload.SlotMinutes;
        activity.SeatsPerSlot = payload.SeatsPerSlot;
        activity.RowVersion = payload.RowVersion;
    }
}

/// <summary>
/// The parallel activities of an event in person (design M4 §4-bis, §7.2, E17; note 2026-10-15-le-attivita-e-la-conservazione-breve §1): a
/// resource of the CRUD engine filtered by <c>filter[eventId]</c>, read with <c>EventBookings.View</c> and written with
/// <c>EventBookings.Edit</c> on the event's care — the area the catalogue gives them (§6.1; the note's question 1). The tab «Activities» of
/// the event's page is a generated list with the seats taken, and each activity a generated form. No hand written verb.
/// </summary>
public static class EventActivityEndpoints
{
    public const string Pattern = "/api/events/activities";

    public static IEndpointRouteBuilder MapEventActivityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapCrud<EventActivity, EventActivityDto, EventActivityDto, EventActivityWriteDto>(Pattern, options =>
        {
            options.PermissionArea = EventsPermissions.BookingsArea;
            options.Name = "EventActivities";
            options.ReadPolicy = EventsPermissions.BookingsView;
            options.WritePolicy = EventsPermissions.BookingsEdit;
            options.ContextType = typeof(EventsDbContext);

            // By the hour they start, as the event's page lists them.
            options.DefaultOrder = activity => activity.FromUtc;
            options.Sortable.Add(nameof(EventActivity.FromUtc));
            options.Filterable.Add(nameof(EventActivity.EventId));

            options.ToList = ActivityMapper.ToDto;
            options.ToListPage = PageAsync;
            options.ToDetail = ActivityMapper.ToDto;
            options.Apply = ActivityMapper.Apply;
            options.BeforeAuthorize = async (activity, saving) =>
                (await saving.Services.GetRequiredService<EventChildren>().AdoptAsync(activity, saving.CancellationToken)).Refusal;
            options.BeforeSave = SaveAsync;
            options.Delete = DeleteAsync;
        });

        return app;
    }

    /// <summary>
    /// An activity belongs to an event in person, which has at most <see cref="EventActivity.MaxPerEvent"/> (reading 4), inside its window,
    /// with at least one turn and at most <see cref="EventActivity.MaxTurns"/> (reading 3). Once members took seats in it, it changes only so
    /// that every seat taken still falls on one of its turns and no turn holds more seats than it offers (reading 5).
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> SaveAsync(EventActivity activity, CrudSaving saving)
    {
        var database = (EventsDbContext)saving.Database;

        if (await saving.Services.GetRequiredService<EventChildren>().EventAsync(activity.EventId, saving.CancellationToken) is not { } parent)
        {
            return EventChildren.Refusal("eventId", "events:errors.eventUnknown");
        }

        if (!parent.InPerson)
        {
            return EventChildren.Refusal("eventId", "events:errors.activityNotInPerson");
        }

        if (saving.IsNew
            && await database.Activities.CountAsync(row => row.EventId == activity.EventId, saving.CancellationToken) >= EventActivity.MaxPerEvent)
        {
            return EventChildren.Refusal("eventId", "events:errors.activitiesTooMany");
        }

        if (activity.FromUtc < parent.StartsAtUtc || activity.ToUtc > parent.EndsAtUtc)
        {
            return EventChildren.Refusal("fromUtc", "events:errors.activityOutsideEvent");
        }

        var turns = activity.Turns().Count;
        if (turns == 0)
        {
            return EventChildren.Refusal("slotMinutes", "events:errors.activityNoTurn");
        }

        if (turns > EventActivity.MaxTurns)
        {
            return EventChildren.Refusal("slotMinutes", "events:errors.activityTooManyTurns");
        }

        if (saving.IsNew)
        {
            return null;
        }

        var taken = await CrudSource.BackOffice<EventActivitySeat>(database).AsNoTracking()
            .Where(seat => seat.ActivityId == activity.Id)
            .Select(seat => seat.StartsAtUtc)
            .ToListAsync(saving.CancellationToken);

        if (taken.Any(start => !activity.IsTurn(start)))
        {
            return EventChildren.Refusal("slotMinutes", "events:errors.activitySeatsTaken");
        }

        return taken.GroupBy(start => start).Any(turn => turn.Count() > activity.SeatsPerSlot)
            ? EventChildren.Refusal("seatsPerSlot", "events:errors.activitySeatsTaken")
            : null;
    }

    /// <summary>
    /// An activity with seats taken is not deleted (the note's question 2, (A)), as an event with registrations is not: the members hold its
    /// turns. One nobody took a seat in goes, through the unit of work, with its row in the audit.
    /// </summary>
    private static async Task DeleteAsync(EventActivity activity, IServiceProvider services, CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<EventsDbContext>();

        if (await CrudSource.BackOffice<EventActivitySeat>(database).AnyAsync(seat => seat.ActivityId == activity.Id, cancellationToken))
        {
            throw new DomainRefusalException("id", "events:errors.activityHasSeats");
        }

        database.Activities.Remove(activity);
    }

    /// <summary>A page of activities with the seats taken in each: one query for the page, or the statistics of an event past its retention.</summary>
    private static async Task<IReadOnlyList<EventActivityDto>> PageAsync(
        IReadOnlyList<EventActivity> activities,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var taken = await services.GetRequiredService<InPersonRetention>()
            .SeatsTakenAsync([.. activities.Select(activity => activity.EventId).Distinct()], cancellationToken);

        return [.. activities.Select(activity => ActivityMapper.Row(activity, taken.GetValueOrDefault(activity.Id)))];
    }
}
