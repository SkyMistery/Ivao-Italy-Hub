using System.Text.Json;
using IvaoHub.Core.Division;

namespace IvaoHub.Modules.Events;

/// <summary>A slot is prepared by the staff with its flight, or generated from the capacity of an airport (design M4 §1.5).</summary>
public enum SlotKind
{
    /// <summary>Prepared by the staff — loaded from a sheet, or written in its form — with its flight: a pilot books it as it is.</summary>
    Public,

    /// <summary>Generated from the capacity of an airport of the event (§3.2, E7): an airport, a direction, a time.</summary>
    Private,
}

/// <summary>
/// A slot of an event (design M4 §1.5), <c>evt_slots</c>: an off block or an on block time at an airport of the event, which one
/// pilot books (E6a). Born whole in E5, for the public slots and for the private ones E7 generates, so that no later phase
/// migrates it again.
/// <para>A <b>public</b> slot is a flight the staff publishes — callsign, flight number, the aircraft types allowed, from, off
/// block, to, on block, the stand — and may be a leg of a <b>rotation</b> (§3.1): a chain of legs, each one leaving from where the
/// one before it arrived, which a pilot books whole, in pieces or one leg at a time. Its airport of the event and its direction
/// are read off its two airports: a departure from an airport of the event, or else an arrival at one. A <b>private</b> slot has
/// only its airport, its direction and its time there, in <see cref="OffBlockUtc"/> for a departure and <see cref="OnBlockUtc"/> for
/// an arrival; the pilot writes the rest when booking (E7).</para>
/// <para>A row of the staff that belongs to its event (<see cref="IEventChild"/>), in the area of the bookings: whoever writes the
/// routes of an event does not write its slots (§6.1). No booking is on this row: a booking is a row of its own (E6a), so a slot
/// is free while no booking names it.</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.BookingsArea)]
public sealed class EventSlot : IEventChild, IAuditable, IHasResourceScope
{
    /// <summary>The longest callsign and flight number: what a flight plan holds, with room to spare.</summary>
    public const int MaxCodeLength = 16;

    /// <summary>The longest stand: «T1 C14», «TBD», a gate's name.</summary>
    public const int MaxStandLength = 32;

    /// <summary>The longest code of a rotation: any code the staff chooses.</summary>
    public const int MaxRotationLength = 32;

    /// <summary>The most legs a rotation has: a day of an aircraft, with room to spare.</summary>
    public const int MaxLeg = 99;

    /// <summary>The most aircraft types a slot allows: a family, not a fleet.</summary>
    public const int MaxAircraftTypes = 10;

    private static readonly JsonSerializerOptions ColumnJson = new(JsonSerializerDefaults.Web);

    public long Id { get; set; }

    public long EventId { get; set; }

    public SlotKind Kind { get; set; }

    /// <summary>The airport of the event the slot is at, upper case: for a public slot, read off its two airports.</summary>
    public string EventAirportIcao { get; set; } = string.Empty;

    /// <summary>An arrival at <see cref="EventAirportIcao"/>, or a departure from it.</summary>
    public bool IsArrival { get; set; }

    /// <summary>Public: the callsign the pilot flies with, upper case. Private: empty — the pilot's is on the booking (E7).</summary>
    public string? Callsign { get; set; }

    /// <summary>Public: the flight number, upper case, when the staff gives one.</summary>
    public string? FlightNumber { get; set; }

    /// <summary>The aircraft types allowed, as a JSON array of codes: the column.</summary>
    public string AircraftTypesJson { get; set; } = "[]";

    /// <summary>Public: one or more aircraft types the core knows, upper case. Private: none — the pilot declares it (E7).</summary>
    public IReadOnlyList<string> AircraftTypes
    {
        get => JsonSerializer.Deserialize<List<string>>(AircraftTypesJson, ColumnJson) ?? [];
        set => AircraftTypesJson = JsonSerializer.Serialize(value ?? [], ColumnJson);
    }

    /// <summary>Public: where the flight leaves from, an airport the core knows.</summary>
    public string? DepartureIcao { get; set; }

    /// <summary>Public: where the flight goes, an airport the core knows.</summary>
    public string? ArrivalIcao { get; set; }

    /// <summary>Public: the off block time (EOBT). Private: the time at the airport of the event of a departure.</summary>
    public DateTime? OffBlockUtc { get; set; }

    /// <summary>Public: the on block time (EIBT). Private: the time at the airport of the event of an arrival.</summary>
    public DateTime? OnBlockUtc { get; set; }

    /// <summary>Public: the stand, as the staff writes it. Private: empty until the stands are managed (§0.2).</summary>
    public string? Stand { get; set; }

    /// <summary>Public: the rotation the leg belongs to, any code the staff chooses; empty for a slot alone.</summary>
    public string? RotationCode { get; set; }

    /// <summary>Public: the place of the leg in its rotation, from 1; empty exactly when <see cref="RotationCode"/> is.</summary>
    public int? RotationLeg { get; set; }

    /// <summary>Private: made by the generator (§3.2), which replaces the free ones it made when it runs again.</summary>
    public bool Generated { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>The event's: a permission granted on one event reaches its slots.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);
}
