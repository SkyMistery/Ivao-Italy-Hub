using System.Text.RegularExpressions;
using FluentValidation;
using IvaoHub.Modules.FlightOps.Tours;

namespace IvaoHub.Modules.FlightOps.Legs;

/// <summary>
/// One row of the leg editor (design M2 §8.4): the leg as stored, plus what is computed at the read — the IATA codes
/// a pilot recognises, the estimated time when the tour names a reference aircraft (§1.5), and whether a report points
/// at it, which decides between deleting and retiring (§1.4.1).
/// </summary>
public sealed record LegDto(
    long Id,
    long TourId,
    int Number,
    LegKind Kind,
    long? RotationId,
    int? SeqInRotation,
    string DepartureIcao,
    string? DepartureIata,
    string ArrivalIcao,
    string? ArrivalIata,
    decimal DistanceNm,
    int? EstimatedMinutes,
    string? RealCallsign,
    string? FlightNumber,
    AllowedAircraft Aircraft,
    DateTime? ReleaseAt,
    DateTime? RetiredAt,
    string? RetiredReason,
    bool HasReports,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// The legs of a tour, all of them, retired ones included — the editor shows those with their reason — and the totals
/// of the ones still flown: the distance, and the estimated time when there is one.
/// </summary>
public sealed record TourLegsDto(
    long TourId,
    IReadOnlyList<LegDto> Legs,
    decimal TotalNm,
    int? TotalEstimatedMinutes);

/// <summary>
/// What a client may set on a leg. The coordinates and the distance are the server's; the kind and the rotation are
/// written by the hub tours (T7b). <c>ChangeReason</c> is required when the leg has reports, and goes to the audit.
/// </summary>
public sealed record LegWriteDto(
    string DepartureIcao,
    string ArrivalIcao,
    string? RealCallsign,
    string? FlightNumber,
    AllowedAircraft? Aircraft,
    DateTime? ReleaseAt,
    string? ChangeReason,
    DateTime RowVersion);

/// <summary>What removing a leg will do, decided by the server and said before anybody confirms (design M2 §1.4.1).</summary>
public enum LegRemovalOutcome
{
    /// <summary>No report points at it: deleted, and the legs after it renumbered.</summary>
    Delete,

    /// <summary>A report points at it: retired, with a reason.</summary>
    Retire,

    /// <summary>A report points at it and it is part of a rotation: the whole rotation is retired (Carmine, 15 September 2026).</summary>
    RetireRotation,
}

/// <summary>The answer to "what happens if I remove this leg?": the outcome, and the numbers of every leg it touches.</summary>
public sealed record LegRemovalDto(LegRemovalOutcome Outcome, IReadOnlyList<int> Numbers);

/// <summary>Removing or restoring a leg: the reason, required to retire and to restore, and the version the editor saw.</summary>
public sealed record LegReasonRequest(string? Reason, DateTime RowVersion);

/// <summary>The limits of a leg, shared by the validator and the columns.</summary>
public static partial class LegValidation
{
    public const int MaxCallsignLength = 16;

    public const int MaxReasonLength = 500;

    [GeneratedRegex("^[A-Z0-9]{4}$")]
    public static partial Regex IcaoPattern();

    public static string Normalize(string? icao) => (icao ?? string.Empty).Trim().ToUpperInvariant();
}

/// <summary>
/// The rules one payload can answer by itself. Messages are i18n keys; the fields are those of the payload, so the
/// editor puts a refusal under its cell. Whether the airports exist, and whether a reason is owed, is the server's to
/// see (<see cref="LegBook"/>).
/// </summary>
public sealed class LegWriteDtoValidator : AbstractValidator<LegWriteDto>
{
    public LegWriteDtoValidator()
    {
        RuleFor(leg => leg.DepartureIcao)
            .Must(icao => LegValidation.IcaoPattern().IsMatch(LegValidation.Normalize(icao)))
            .WithMessage("flightops:errors.icao");
        RuleFor(leg => leg.ArrivalIcao)
            .Must(icao => LegValidation.IcaoPattern().IsMatch(LegValidation.Normalize(icao)))
            .WithMessage("flightops:errors.icao");
        RuleFor(leg => leg.RealCallsign).MaximumLength(LegValidation.MaxCallsignLength).WithMessage("errors.text.tooLong");
        RuleFor(leg => leg.FlightNumber).MaximumLength(LegValidation.MaxCallsignLength).WithMessage("errors.text.tooLong");
        RuleFor(leg => leg.ChangeReason).MaximumLength(LegValidation.MaxReasonLength).WithMessage("errors.text.tooLong");
    }
}

public sealed class LegReasonRequestValidator : AbstractValidator<LegReasonRequest>
{
    public LegReasonRequestValidator()
    {
        RuleFor(request => request.Reason).MaximumLength(LegValidation.MaxReasonLength).WithMessage("errors.text.tooLong");
    }
}
