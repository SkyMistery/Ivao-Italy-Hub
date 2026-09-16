namespace IvaoHub.Core.Ivao;

/// <summary>
/// An aircraft type as IVAO lists it (<c>/v2/aircrafts/all</c>, 2626 rows measured on 16 September
/// 2026). It is what a tour means by "the aircraft allowed": a flight plan carries the ICAO code and
/// nothing finer, so this is the vocabulary the editor offers and the check compares against.
/// <para>⚠️ <b>Variants are deliberately not here.</b> <c>/v2/aircrafts/{icao}/variants</c> answers
/// liveries and engine options of the same type (<c>A320w</c>, <c>A320CFM</c>, <c>A320IAE</c>), not
/// related types: <c>A20N</c> is a type of its own and not a variant of <c>A320</c>. A flight plan
/// never carries a variant either. A tour that wants the neos alongside the ceos says so with an
/// aircraft group (design M2 section 1.5), which is the mechanism that actually expresses it.</para>
/// </summary>
public sealed class IvaoAircraftType
{
    /// <summary>ICAO type designator, for example <c>A320</c> or <c>E55P</c>.</summary>
    public string IcaoCode { get; set; } = string.Empty;

    public string? IataCode { get; set; }

    /// <summary>The model as IVAO writes it, for example <c>EMB-505 Phenom 300</c>.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Airbus, Boeing, Embraer… flattened from the object IVAO nests.</summary>
    public string? Manufacturer { get; set; }

    /// <summary>LandPlane, Helicopter, Gyrocopter, Amphibian…</summary>
    public string? Description { get; set; }

    /// <summary>Wake turbulence category: <c>L</c>, <c>M</c>, <c>H</c>, <c>J</c>.</summary>
    public string? WakeTurbulence { get; set; }

    public int? NumberOfEngines { get; set; }

    /// <summary><c>civil</c> or <c>military</c>, as IVAO puts it.</summary>
    public string? Military { get; set; }

    public string RawJson { get; set; } = "{}";

    public DateTime SyncedAt { get; set; }
}

/// <summary>
/// One equipment letter of a flight plan (<c>S</c> standard, <c>D</c> DME, <c>J1</c> CPDLC…), from
/// <c>/v2/aircrafts/equipments</c>. Thirty six rows, and they are a <b>vocabulary</b>, not data about
/// a flight: the rule that says which letters a tour requires is written by picking from these, and
/// the check reads the letters of the plan itself, which IVAO already expands for us.
/// </summary>
public sealed class IvaoAircraftEquipment
{
    /// <summary>The letter, or letter and digit: <c>S</c>, <c>D</c>, <c>J1</c>.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime SyncedAt { get; set; }
}

/// <summary>The transponder vocabulary, from <c>/v2/aircrafts/transponderTypes</c> (seventeen rows).</summary>
public sealed class IvaoTransponderType
{
    /// <summary>The letter: <c>C</c>, <c>S</c>, <c>N</c>…</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>What IVAO calls its type, when it says so.</summary>
    public string? Kind { get; set; }

    public int Order { get; set; }

    public DateTime SyncedAt { get; set; }
}
