using System.Globalization;
using FluentValidation;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Modules;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Settings;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>What a load does with the public slots already there (design M4 §3.1).</summary>
public enum SlotLoadMode
{
    /// <summary>They stay, and the table's slots join them.</summary>
    Add,

    /// <summary>The free public ones go, and the table's take their place; a booked one stays (E6a).</summary>
    ReplaceFree,
}

/// <summary>The table, as pasted or as the file the browser read, and what to do with the public slots already there.</summary>
public sealed record SlotLoadRequest(string? Text, SlotLoadMode Mode);

/// <summary>How many slots the load wrote, and how many free public ones it took away first.</summary>
public sealed record SlotLoadResultDto(int Added, int Removed);

/// <summary>How many free slots «delete the free ones» took away.</summary>
public sealed record SlotsDeletedDto(int Removed);

/// <summary>The limits of one request; what each row says is the load's to check, row by row.</summary>
public sealed class SlotLoadRequestValidator : AbstractValidator<SlotLoadRequest>
{
    /// <summary>A thousand rows of a dozen cells, with room to spare: more is a file of something else.</summary>
    public const int MaxTextLength = 500_000;

    public SlotLoadRequestValidator()
    {
        RuleFor(request => request.Text).NotEmpty().WithMessage("errors.required");
        RuleFor(request => request.Text).MaximumLength(MaxTextLength).WithMessage("errors.text.tooLong");
        RuleFor(request => request.Mode).IsInEnum().WithMessage("errors.required");
    }
}

/// <summary>
/// The free slots of an event: the slots no booking names (E6a). The load's «replace» and «delete the free ones» ask here, so a booked
/// slot is never taken away by either. The bookings are read whoever asks — a booking is a member's row, which the global filter
/// hides from a visitor and a job —, in one query.
/// </summary>
internal static class SlotRows
{
    public static IQueryable<EventSlot> Free(EventsDbContext database, long eventId)
    {
        var bookings = CrudSource.BackOffice<EventBooking>(database);
        return database.Slots.Where(slot => slot.EventId == eventId && !bookings.Any(booking => booking.SlotId == slot.Id));
    }
}

/// <summary>
/// The load of the public slots of an event from a table (design M4 §3.1, note 2026-09-29-gli-slot-e-le-prenotazioni §2.3, E5):
/// all or nothing, every refusal on the row and the column it is about (<c>rows[12].aircraft_types</c>), in one transaction.
/// <para>Every row is a public slot: its callsign, its flight number if any, its aircraft types — each one the core knows —, its
/// two airports — ones the core knows, one of them of the event, which gives the slot its airport and its direction
/// (<see cref="SlotDirection"/>) —, its off block and on block times in UTC, its time at the airport of the event inside the event's
/// window with a margin (<see cref="SlotWindow"/>), its stand if any, and its rotation and place if any.
/// The callsign and the off block are one slot of the event once, among the table's and the slots that stay. Then the rotations
/// (<see cref="SlotChains"/>), with the minutes the settings put between two bookings of a pilot.</para>
/// <para>It adds the table's slots to those there, or replaces the free public ones with them (<see cref="SlotLoadMode"/>). The event
/// must have public slots and airports of its own.</para>
/// </summary>
public sealed class SlotLoading(
    EventsDbContext database,
    IAirportDirectory airports,
    IAircraftTypeDirectory aircraft,
    ModuleSettingsStore settings)
{
    public async Task<(SlotLoadResultDto? Result, Refusals Problems)> LoadAsync(
        Event row,
        SlotLoadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(request);

        var problems = new Refusals();

        // Public slots are what the switch of the event says it has (§1.2); and a slot is always at an airport of its event.
        if (!row.PublicSlots)
        {
            return (null, problems.Add(SlotSheet.TextField, "events:errors.noPublicSlots"));
        }

        var eventAirports = await database.Airports.AsNoTracking()
            .Where(airport => airport.EventId == row.Id)
            .Select(airport => airport.Icao)
            .ToListAsync(cancellationToken);

        if (eventAirports.Count == 0)
        {
            return (null, problems.Add(SlotSheet.TextField, EventPublishing.SlotsNeedAirportsKey));
        }

        var sheet = SlotSheet.Read(request.Text);
        if (!sheet.Problems.IsEmpty)
        {
            return (null, sheet.Problems);
        }

        // Each row read on its own: what a row cannot say is refused on its cell, and the row goes no further.
        var drafts = new List<SlotDraft>();
        var unreadRotations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sheetRow in sheet.Rows)
        {
            if (SlotDraft.Read(sheetRow, problems) is { } draft)
            {
                drafts.Add(draft);
            }
            else if (sheetRow[SlotColumns.Rotation] is { Length: > 0 } rotation)
            {
                unreadRotations.Add(rotation);
            }
        }

        // What the core knows, asked once for the whole table.
        var known = await airports.FindAsync(
            [.. drafts.SelectMany(draft => new[] { draft.DepartureIcao, draft.ArrivalIcao }).Distinct(StringComparer.Ordinal)],
            cancellationToken);
        var unknownTypes = (await aircraft.UnknownAsync(
                [.. drafts.SelectMany(draft => draft.AircraftTypes).Distinct(StringComparer.Ordinal)],
                cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var stored = await database.Slots.Where(slot => slot.EventId == row.Id).ToListAsync(cancellationToken);
        var leaving = request.Mode == SlotLoadMode.ReplaceFree
            ? (await SlotRows.Free(database, row.Id).Where(slot => slot.Kind == SlotKind.Public).Select(slot => slot.Id).ToListAsync(cancellationToken))
                .ToHashSet()
            : [];
        var staying = stored.Where(slot => !leaving.Contains(slot.Id)).ToList();

        var taken = staying
            .Where(slot => slot.Callsign is not null && slot.OffBlockUtc is not null)
            .Select(slot => (slot.Callsign!, slot.OffBlockUtc!.Value))
            .ToHashSet();

        var directions = new Dictionary<int, SlotDirection>();
        foreach (var draft in drafts)
        {
            var field = (string column) => SlotColumns.Field(draft.Row, column);

            if (!known.ContainsKey(draft.DepartureIcao))
            {
                problems.Add(field(SlotColumns.DepartureIcao), "events:errors.airportUnknown");
            }

            if (!known.ContainsKey(draft.ArrivalIcao))
            {
                problems.Add(field(SlotColumns.ArrivalIcao), "events:errors.airportUnknown");
            }

            if (draft.AircraftTypes.Any(unknownTypes.Contains))
            {
                problems.Add(field(SlotColumns.AircraftTypes), "events:errors.aircraftUnknown");
            }

            if (SlotDirection.Of(draft.DepartureIcao, draft.ArrivalIcao, eventAirports) is { } direction)
            {
                directions[draft.Row] = direction;

                // Days away from the event is a typing mistake: refused on the time at its airport (point 10 on #228).
                if (!SlotWindow.Holds(SlotWindow.AtTheEvent(direction, draft.OffBlockUtc, draft.OnBlockUtc), row.StartsAtUtc, row.EndsAtUtc))
                {
                    problems.Add(field(direction.IsArrival ? SlotColumns.OnBlockUtc : SlotColumns.OffBlockUtc), SlotWindow.OutsideKey);
                }
            }
            else
            {
                problems.Add(field(SlotColumns.DepartureIcao), "events:errors.slotAwayFromEvent");
            }

            // One flight of the event at one off block time: among the slots that stay, and among the table's own rows.
            if (!taken.Add((draft.Callsign, draft.OffBlockUtc)))
            {
                problems.Add(field(SlotColumns.Callsign), "events:errors.slotTwice");
            }
        }

        var gap = (await settings.GetAsync<EventsSettings>(EventsModule.ModuleKey, cancellationToken)).BookingGapMinutes;
        var chains = SlotChains.Check(
            [
                .. staying
                    .Where(slot => slot is { Kind: SlotKind.Public, RotationCode: not null, DepartureIcao: not null, ArrivalIcao: not null }
                        && slot.OffBlockUtc is not null && slot.OnBlockUtc is not null)
                    .Select(slot => new ChainLeg(
                        slot.Id,
                        IsNew: false,
                        slot.RotationCode!,
                        slot.RotationLeg,
                        slot.DepartureIcao!,
                        slot.ArrivalIcao!,
                        slot.OffBlockUtc!.Value,
                        slot.OnBlockUtc!.Value)),
                .. drafts
                    .Where(draft => draft.Rotation is not null && !unreadRotations.Contains(draft.Rotation))
                    .Select(draft => new ChainLeg(
                        draft.Row,
                        IsNew: true,
                        draft.Rotation!,
                        draft.Leg,
                        draft.DepartureIcao,
                        draft.ArrivalIcao,
                        draft.OffBlockUtc,
                        draft.OnBlockUtc)),
            ],
            gap);

        foreach (var problem in chains.Problems)
        {
            problems.Add(SlotColumns.Field((int)problem.Leg.Key, problem.Column), problem.Key);
        }

        if (!problems.IsEmpty)
        {
            return (null, problems);
        }

        // All of it, in one save: the free public slots that go, and the table's — in the event's care, as every row of its staff.
        database.Slots.RemoveRange(stored.Where(slot => leaving.Contains(slot.Id)));
        database.Slots.AddRange(drafts.Select(draft => new EventSlot
        {
            EventId = row.Id,
            Kind = SlotKind.Public,
            EventAirportIcao = directions[draft.Row].EventAirportIcao,
            IsArrival = directions[draft.Row].IsArrival,
            Callsign = draft.Callsign,
            FlightNumber = draft.FlightNumber,
            AircraftTypes = draft.AircraftTypes,
            DepartureIcao = draft.DepartureIcao,
            ArrivalIcao = draft.ArrivalIcao,
            OffBlockUtc = draft.OffBlockUtc,
            OnBlockUtc = draft.OnBlockUtc,
            Stand = draft.Stand,
            RotationCode = draft.Rotation,
            RotationLeg = draft.Rotation is null ? null : draft.Leg ?? chains.Assigned[draft.Row],
            OwnerDepartment = row.OwnerDepartment,
            OwnerDepartmentMask = row.OwnerDepartmentMask,
        }));

        await database.SaveChangesAsync(cancellationToken);

        return (new SlotLoadResultDto(drafts.Count, leaving.Count), problems);
    }
}

/// <summary>
/// One row of the table read as a public slot, before the core and the other rows are asked: every cell in its shape, or refused on
/// its cell (<c>rows[12].off_block_utc</c>) — and then no slot.
/// </summary>
public sealed record SlotDraft(
    int Row,
    string Callsign,
    string? FlightNumber,
    IReadOnlyList<string> AircraftTypes,
    string DepartureIcao,
    string ArrivalIcao,
    DateTime OffBlockUtc,
    DateTime OnBlockUtc,
    string? Stand,
    string? Rotation,
    int? Leg)
{
    public static SlotDraft? Read(SlotSheetRow row, Refusals problems)
    {
        var read = true;

        void Refuse(string column, string key)
        {
            problems.Add(SlotColumns.Field(row.Number, column), key);
            read = false;
        }

        var callsign = row[SlotColumns.Callsign].ToUpperInvariant();
        if (callsign.Length == 0)
        {
            Refuse(SlotColumns.Callsign, "errors.required");
        }
        else if (callsign.Length > EventSlot.MaxCodeLength)
        {
            Refuse(SlotColumns.Callsign, "errors.text.tooLong");
        }
        else if (!SlotValues.IsCallsign(callsign))
        {
            Refuse(SlotColumns.Callsign, "events:errors.callsignFormat");
        }

        var flightNumber = row[SlotColumns.FlightNumber].ToUpperInvariant();
        if (flightNumber.Length > EventSlot.MaxCodeLength)
        {
            Refuse(SlotColumns.FlightNumber, "errors.text.tooLong");
        }

        var types = SlotValues.AircraftTypes(row[SlotColumns.AircraftTypes]);
        if (types.Count == 0)
        {
            Refuse(SlotColumns.AircraftTypes, "errors.required");
        }
        else if (types.Count > EventSlot.MaxAircraftTypes)
        {
            Refuse(SlotColumns.AircraftTypes, "events:errors.aircraftTooMany");
        }
        else if (!types.All(SlotValues.IsAircraftType))
        {
            Refuse(SlotColumns.AircraftTypes, "events:errors.aircraftFormat");
        }

        var departure = Airport(SlotColumns.DepartureIcao);
        var arrival = Airport(SlotColumns.ArrivalIcao);
        if (departure is not null && departure == arrival)
        {
            Refuse(SlotColumns.ArrivalIcao, "events:errors.slotToItself");
        }

        var offBlock = Instant(SlotColumns.OffBlockUtc);
        var onBlock = Instant(SlotColumns.OnBlockUtc);
        if (offBlock is not null && onBlock is not null && onBlock <= offBlock)
        {
            Refuse(SlotColumns.OnBlockUtc, "events:errors.onBlockBeforeOffBlock");
        }

        var stand = row[SlotColumns.Stand];
        if (stand.Length > EventSlot.MaxStandLength)
        {
            Refuse(SlotColumns.Stand, "errors.text.tooLong");
        }

        var rotation = row[SlotColumns.Rotation];
        if (rotation.Length > EventSlot.MaxRotationLength)
        {
            Refuse(SlotColumns.Rotation, "errors.text.tooLong");
        }

        int? leg = null;
        if (row[SlotColumns.Leg] is { Length: > 0 } place)
        {
            if (int.TryParse(place, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is >= 1 and <= EventSlot.MaxLeg)
            {
                leg = number;
            }
            else
            {
                Refuse(SlotColumns.Leg, "events:errors.legRange");
            }

            if (rotation.Length == 0)
            {
                Refuse(SlotColumns.Leg, "events:errors.legWithoutRotation");
            }
        }

        return read
            ? new SlotDraft(
                row.Number,
                callsign,
                flightNumber.Length == 0 ? null : flightNumber,
                types,
                departure!,
                arrival!,
                offBlock!.Value,
                onBlock!.Value,
                stand.Length == 0 ? null : stand,
                rotation.Length == 0 ? null : rotation,
                leg)
            : null;

        string? Airport(string column)
        {
            var code = row[column].ToUpperInvariant();
            if (code.Length == 0)
            {
                Refuse(column, "errors.required");
                return null;
            }

            if (!SlotValues.IsAirport(code))
            {
                Refuse(column, "events:errors.airportUnknown");
                return null;
            }

            return code;
        }

        DateTime? Instant(string column)
        {
            var cell = row[column];
            if (cell.Length == 0)
            {
                Refuse(column, "errors.required");
                return null;
            }

            var instant = SlotValues.Instant(cell);
            if (instant is null)
            {
                Refuse(column, "events:errors.instantFormat");
            }

            return instant;
        }
    }
}
