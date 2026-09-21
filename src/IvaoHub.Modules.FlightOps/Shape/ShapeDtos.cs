using FluentValidation;
using IvaoHub.Core.Division;
using IvaoHub.Modules.FlightOps.Legs;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Modules.FlightOps.Shape;

/// <summary>A hub as the list and the form show it.</summary>
public sealed record HubDto(
    long Id,
    long TourId,
    Department OwnerDepartment,
    string Icao,
    int Sort,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>What a client may set on a hub. The tour is chosen when it is created; the department is the tour's.</summary>
public sealed record HubWriteDto(long TourId, string Icao, int Sort, DateTime RowVersion);

/// <summary>A rotation as the form loads it.</summary>
public sealed record RotationDto(
    long Id,
    long TourId,
    Department OwnerDepartment,
    long HubId,
    int Sort,
    int Size,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>A rotation as the list shows it: its hub, and how many of its legs are still flown out of how many it needs.</summary>
public sealed record RotationListDto(
    long Id,
    long TourId,
    Department OwnerDepartment,
    long HubId,
    string? HubIcao,
    int Sort,
    int Size,
    int Legs,
    DateTime UpdatedAt,
    DateTime RowVersion);

public sealed record RotationWriteDto(long TourId, long HubId, int Sort, int Size, DateTime RowVersion);

/// <summary>A callsign constraint as the form loads it.</summary>
public sealed record CallsignRuleDto(
    long Id,
    long TourId,
    Department OwnerDepartment,
    long? LegId,
    CallsignMode Mode,
    CallsignMatch Match,
    string Value,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>A callsign constraint as the list shows it: the number of its leg, when it is a leg's.</summary>
public sealed record CallsignRuleListDto(
    long Id,
    long TourId,
    Department OwnerDepartment,
    long? LegId,
    int? LegNumber,
    CallsignMode Mode,
    CallsignMatch Match,
    string Value,
    DateTime UpdatedAt,
    DateTime RowVersion);

public sealed record CallsignRuleWriteDto(
    long TourId,
    long? LegId,
    CallsignMode Mode,
    CallsignMatch Match,
    string Value,
    DateTime RowVersion);

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class ShapeMapper
{
    public partial HubDto ToDto(TourHub hub);

    public partial RotationDto ToDto(Rotation rotation);

    public partial RotationListDto ToList(Rotation rotation, string? hubIcao, int legs);

    public partial CallsignRuleDto ToDto(CallsignRule rule);

    public partial CallsignRuleListDto ToList(CallsignRule rule, int? legNumber);

    /// <summary>The tour only on a new row: a hub, a rotation or a constraint never moves to another tour.</summary>
    public void Apply(HubWriteDto payload, TourHub hub)
    {
        if (hub.Id == 0)
        {
            hub.TourId = payload.TourId;
        }

        hub.Icao = LegValidation.Normalize(payload.Icao);
        hub.Sort = payload.Sort;
    }

    public void Apply(RotationWriteDto payload, Rotation rotation)
    {
        if (rotation.Id == 0)
        {
            rotation.TourId = payload.TourId;
        }

        rotation.HubId = payload.HubId;
        rotation.Sort = payload.Sort;
        rotation.Size = payload.Size;
    }

    public void Apply(CallsignRuleWriteDto payload, CallsignRule rule)
    {
        if (rule.Id == 0)
        {
            rule.TourId = payload.TourId;
        }

        rule.LegId = payload.LegId;
        rule.Mode = payload.Mode;
        rule.Match = payload.Match;
        rule.Value = CallsignRules.Normalize(payload.Value);
    }
}

public sealed class HubWriteDtoValidator : AbstractValidator<HubWriteDto>
{
    public HubWriteDtoValidator()
    {
        RuleFor(hub => hub.TourId).GreaterThan(0).WithMessage("errors.required");
        RuleFor(hub => hub.Icao)
            .Must(icao => LegValidation.IcaoPattern().IsMatch(LegValidation.Normalize(icao)))
            .WithMessage("flightops:errors.icao");
        RuleFor(hub => hub.Sort).InclusiveBetween(0, 999).WithMessage("errors.number.range");
    }
}

public sealed class RotationWriteDtoValidator : AbstractValidator<RotationWriteDto>
{
    public RotationWriteDtoValidator()
    {
        RuleFor(rotation => rotation.TourId).GreaterThan(0).WithMessage("errors.required");
        RuleFor(rotation => rotation.HubId).GreaterThan(0).WithMessage("errors.required");
        RuleFor(rotation => rotation.Sort).InclusiveBetween(0, 999).WithMessage("errors.number.range");
        RuleFor(rotation => rotation.Size).Must(Rotation.Sizes.Contains).WithMessage("flightops:errors.rotationSizeChoice");
    }
}

public sealed class CallsignRuleWriteDtoValidator : AbstractValidator<CallsignRuleWriteDto>
{
    public CallsignRuleWriteDtoValidator()
    {
        RuleFor(rule => rule.TourId).GreaterThan(0).WithMessage("errors.required");
        RuleFor(rule => rule.Mode).IsInEnum().WithMessage("errors.required");
        RuleFor(rule => rule.Match).IsInEnum().WithMessage("errors.required");

        // An airline is three letters; a whole callsign only a deny names (note 2026-09-21-la-forma-dei-tour).
        RuleFor(rule => rule.Value)
            .Must(value => CallsignRules.AirlinePattern().IsMatch(CallsignRules.Normalize(value)))
            .When(rule => rule.Match == CallsignMatch.Airline)
            .WithMessage("flightops:errors.airlineCode");
        RuleFor(rule => rule.Value)
            .Must(value => CallsignRules.CallsignPattern().IsMatch(CallsignRules.Normalize(value)))
            .When(rule => rule.Match == CallsignMatch.Exact)
            .WithMessage("flightops:errors.callsign");
        RuleFor(rule => rule.Match)
            .Must((rule, match) => match != CallsignMatch.Exact || rule.Mode == CallsignMode.Deny)
            .WithMessage("flightops:errors.exactOnlyDenies");
    }
}
