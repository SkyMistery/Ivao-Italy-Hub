using FluentValidation;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.FlightOps.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Modules.FlightOps.Aircraft;

/// <summary>A profile as the list and the form show it.</summary>
public sealed record AircraftProfileDto(
    long Id,
    Department OwnerDepartment,
    string IcaoType,
    int CruiseTasKt,
    string? Note,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>What a client may set on a profile. The department is kept by the module's base department.</summary>
public sealed record AircraftProfileWriteDto(
    Department OwnerDepartment,
    string IcaoType,
    int CruiseTasKt,
    string? Note,
    DateTime RowVersion);

/// <summary>A group as the list and the form show it.</summary>
public sealed record AircraftGroupDto(
    long Id,
    Department OwnerDepartment,
    Localized<string> Name,
    IReadOnlyList<string> IcaoTypes,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>What a client may set on a group.</summary>
public sealed record AircraftGroupWriteDto(
    Department OwnerDepartment,
    Localized<string> Name,
    IReadOnlyList<string> IcaoTypes,
    DateTime RowVersion);

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class AircraftMapper
{
    public partial AircraftProfileDto ToDto(AircraftProfile profile);

    public partial void Apply(AircraftProfileWriteDto payload, AircraftProfile profile);

    public partial AircraftGroupDto ToDto(AircraftGroup group);

    [MapperIgnoreTarget(nameof(AircraftGroup.IcaoTypesJson))]
    public partial void Apply(AircraftGroupWriteDto payload, AircraftGroup group);
}

/// <summary>The limits of the aircraft data, shared by the validators and the columns.</summary>
public static class AircraftValidation
{
    public const int MaxNoteLength = 512;

    /// <summary>A group is a handful of types; a hundred is already a list nobody reads.</summary>
    public const int MaxGroupTypes = 100;
}

public sealed class AircraftProfileWriteDtoValidator : AbstractValidator<AircraftProfileWriteDto>
{
    public AircraftProfileWriteDtoValidator()
    {
        RuleFor(profile => profile.IcaoType).NotEmpty().WithMessage("errors.required");
        RuleFor(profile => profile.CruiseTasKt).InclusiveBetween(50, 1500).WithMessage("errors.number.range");
        RuleFor(profile => profile.Note).MaximumLength(AircraftValidation.MaxNoteLength).WithMessage("errors.text.tooLong");
    }
}

public sealed class AircraftGroupWriteDtoValidator : AbstractValidator<AircraftGroupWriteDto>
{
    public AircraftGroupWriteDtoValidator(IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(division);

        RuleFor(group => group.Name).Required(division.Value);
        RuleFor(group => group.IcaoTypes)
            .NotEmpty().WithMessage("errors.required")
            .Must(types => types is null || types.Count <= AircraftValidation.MaxGroupTypes).WithMessage("errors.text.tooLong");
    }
}

/// <summary>
/// The aircraft data of the tours through the generic CRUD engine (design M2 §1.5, §8.7): profiles and
/// groups, read with <c>Tours.View</c> and written with <c>Tours.ManageAircraft</c>, both on the base
/// department of the module. What a validator of one payload cannot see — whether a type exists, whether
/// it already has a profile — is checked before saving.
/// </summary>
public static class AircraftEndpoints
{
    public const string ProfilesPattern = "/api/flightops/aircraft-profiles";

    public const string GroupsPattern = "/api/flightops/aircraft-groups";

    public static IEndpointRouteBuilder MapAircraftEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new AircraftMapper();

        app.MapCrud<AircraftProfile, AircraftProfileDto, AircraftProfileDto, AircraftProfileWriteDto>(ProfilesPattern, options =>
        {
            options.PermissionArea = TourPermissions.Area;
            options.Name = "FlightOpsAircraftProfiles";
            options.ReadPolicy = TourPermissions.View;
            options.WritePolicy = TourPermissions.ManageAircraft;
            options.ContextType = typeof(FlightOpsDbContext);

            options.DefaultOrder = profile => profile.IcaoType;
            options.Sortable.Add(nameof(AircraftProfile.IcaoType));
            options.Sortable.Add(nameof(AircraftProfile.CruiseTasKt));
            options.Sortable.Add(nameof(AircraftProfile.UpdatedAt));
            options.SearchFields.Add(profile => profile.IcaoType);
            options.SearchFields.Add(profile => profile.Note);

            options.ToList = mapper.ToDto;
            options.ToDetail = mapper.ToDto;
            options.Apply = (payload, profile) =>
            {
                mapper.Apply(payload, profile);
                profile.IcaoType = profile.IcaoType.Trim().ToUpperInvariant();
            };
            options.BeforeSave = async (profile, saving) =>
            {
                if ((await saving.Services.GetRequiredService<IAircraftTypeDirectory>()
                        .UnknownAsync([profile.IcaoType], saving.CancellationToken)).Count > 0)
                {
                    return Refusal("icaoType", "errors.aircraft.unknownType");
                }

                var taken = await saving.Database.Set<AircraftProfile>()
                    .AnyAsync(row => row.IcaoType == profile.IcaoType && row.Id != profile.Id, saving.CancellationToken);

                return taken ? Refusal("icaoType", "flightops:errors.profileExists") : null;
            };
        });

        app.MapCrud<AircraftGroup, AircraftGroupDto, AircraftGroupDto, AircraftGroupWriteDto>(GroupsPattern, options =>
        {
            options.PermissionArea = TourPermissions.Area;
            options.Name = "FlightOpsAircraftGroups";
            options.ReadPolicy = TourPermissions.View;
            options.WritePolicy = TourPermissions.ManageAircraft;
            options.ContextType = typeof(FlightOpsDbContext);

            options.DefaultOrder = group => group.Id;
            options.Sortable.Add(nameof(AircraftGroup.UpdatedAt));
            options.SearchFields.Add(group => group.Name);

            options.ToList = mapper.ToDto;
            options.ToDetail = mapper.ToDto;
            options.Apply = mapper.Apply;
            options.BeforeSave = async (group, saving) =>
            {
                var unknown = await saving.Services.GetRequiredService<IAircraftTypeDirectory>()
                    .UnknownAsync(group.IcaoTypes, saving.CancellationToken);

                return unknown.Count > 0 ? Refusal("icaoTypes", "errors.aircraft.unknownType") : null;
            };
        });

        return app;
    }

    private static IReadOnlyDictionary<string, string[]> Refusal(string field, string key) =>
        new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [key] };
}
