using System.Text.Json;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.FlightOps.Aircraft;

/// <summary>
/// How fast one aircraft type flies, as the flight operations department writes it (design M2 §1.5): the
/// speed the estimated time of a leg divides by. One profile per type, for every tour.
/// </summary>
[Audited]
[PermissionArea(TourPermissions.Area)]
public sealed class AircraftProfile : IOwnedByDepartment, IAuditable
{
    public long Id { get; set; }

    /// <summary>The ICAO type designator, one of the types the core knows.</summary>
    public string IcaoType { get; set; } = string.Empty;

    /// <summary>True air speed in cruise, in knots.</summary>
    public int CruiseTasKt { get; set; }

    public string? Note { get; set; }

    /// <summary>The base department of the module, written by the interceptor.</summary>
    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }
}

/// <summary>
/// A named set of aircraft types — «Bizjet», «Airliner», «Historic» — usable wherever aircraft are chosen
/// (design M2 §1.5). It is also how a tour admits the neos with the ceos: IVAO's «variants» are liveries
/// and engines of one type, not related types (T1). Changing a group changes every tour that uses it.
/// </summary>
[Audited]
[PermissionArea(TourPermissions.Area)]
public sealed class AircraftGroup : IOwnedByDepartment, IAuditable
{
    private static readonly JsonSerializerOptions TypesJson = new(JsonSerializerDefaults.Web);

    public long Id { get; set; }

    public Localized<string> Name { get; set; } = Localized<string>.Empty;

    /// <summary>The types, as a JSON array: the column.</summary>
    public string IcaoTypesJson { get; set; } = "[]";

    /// <summary><see cref="IcaoTypesJson"/>, read and written as the list it is: upper case, each once, in order.</summary>
    public IReadOnlyList<string> IcaoTypes
    {
        get => JsonSerializer.Deserialize<string[]>(IcaoTypesJson, TypesJson) ?? [];
        set => IcaoTypesJson = JsonSerializer.Serialize(
            (value ?? []).Select(type => type.Trim().ToUpperInvariant()).Where(type => type.Length > 0).Distinct().Order().ToArray(),
            TypesJson);
    }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }
}
