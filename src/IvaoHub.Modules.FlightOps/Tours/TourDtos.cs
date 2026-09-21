using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentValidation;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Riok.Mapperly.Abstractions;

namespace IvaoHub.Modules.FlightOps.Tours;

/// <summary>A tour as the list shows it: the state is computed, never stored (design M2 §1.2.1, §8.3).</summary>
public sealed record TourListDto(
    long Id,
    Department OwnerDepartment,
    bool IsTemplate,
    string? Slug,
    TourKind Kind,
    Localized<string> Title,
    TourStateKind State,
    DateTime? ReleaseAt,
    DateTime? CloseAt,
    bool IsHidden,
    DateTime UpdatedAt,
    long? ParentTourId);

/// <summary>
/// A tour as its editor loads it. <c>State</c> is what the dates say now (design M2 §1.2.1); <c>IsPublic</c>, whether
/// anybody outside the staff sees it now — from then on its kind no longer changes. On a subtour the dates are the ones
/// in force, and <c>ReleaseFromParent</c> and <c>CloseFromParent</c> say which of them are its container's.
/// </summary>
public sealed record TourDetailDto(
    long Id,
    Department OwnerDepartment,
    bool IsTemplate,
    string? Slug,
    TourKind Kind,
    Localized<string> Title,
    Localized<string> Summary,
    JsonNode Briefing,
    long? CoverMediaId,
    long? BannerMediaId,
    PublishStatus Status,
    DateTime? PublishedAt,
    TourStateKind State,
    bool IsPublic,
    bool IsHidden,
    bool ShowPreview,
    DateTime? ReleaseAt,
    DateTime? CloseAt,
    int ReportWindowDays,
    TourProgression Progression,
    HubRotationOrder? HubRotationOrder,
    bool RequiresProcedures,
    int? DailyLegLimit,
    int? MinPilotRating,
    string? ReferenceAircraftIcao,
    int? RequiredNm,
    AllowedAircraft AllowedAircraft,
    long? AwardId,
    DateTime UpdatedAt,
    DateTime RowVersion,
    long? ParentTourId,
    int? RequiredSubtours,
    bool ReleaseFromParent,
    bool CloseFromParent);

/// <summary>
/// What a client may set on a tour. The state is not here — marking ready, back to draft, hiding and showing are
/// actions (<see cref="TourStatusRequest"/>). Of the shape of a tour, T7a writes the distance of a <c>Distance</c> tour
/// and the aircraft admitted (types and groups, design M2 §1.5); T7b the container and its subtours — the parent is
/// chosen when a subtour is created and never changes, and a subtour's empty date is its container's (note
/// 2026-09-21-la-forma-dei-tour); the goal of an <c>Open</c> tour is T7c's. A null <c>Briefing</c> keeps the briefing as
/// it is; a null <c>ReportWindowDays</c> on a new tour takes the division's default (§1.11); a null
/// <c>AllowedAircraft</c> admits every aircraft.
/// </summary>
public sealed record TourWriteDto(
    Department OwnerDepartment,
    bool IsTemplate,
    string? Slug,
    TourKind Kind,
    Localized<string> Title,
    Localized<string> Summary,
    JsonNode? Briefing,
    long? CoverMediaId,
    long? BannerMediaId,
    bool ShowPreview,
    DateTime? ReleaseAt,
    DateTime? CloseAt,
    int? ReportWindowDays,
    TourProgression Progression,
    HubRotationOrder? HubRotationOrder,
    bool RequiresProcedures,
    int? DailyLegLimit,
    int? MinPilotRating,
    string? ReferenceAircraftIcao,
    int? RequiredNm,
    AllowedAircraft? AllowedAircraft,
    long? AwardId,
    DateTime RowVersion,
    long? ParentTourId = null,
    int? RequiredSubtours = null);

/// <summary>The four things that happen to a tour without its form (design M2 §8.3).</summary>
public enum TourStatusAction
{
    /// <summary>Mark it ready, if nothing stands in the way (§1.2.1).</summary>
    Ready,

    /// <summary>Back to draft, only before its release.</summary>
    Draft,

    /// <summary>Hidden from everybody outside the staff (§1.2.2).</summary>
    Hide,

    Show,
}

public sealed record TourStatusRequest(TourStatusAction Action);

/// <summary>What stands between a draft and "ready", without marking it: the same checks, nothing written.</summary>
public sealed record TourReadyProblemsDto(
    IReadOnlyDictionary<string, string[]> Errors,
    IReadOnlyDictionary<string, string[]> Localized);

/// <summary>A new tour out of a template: the settings come from the template, the name and address from here.</summary>
public sealed record TourFromTemplateRequest(Localized<string> Title, string Slug);

/// <summary>A new template out of a tour: its settings, under this name.</summary>
public sealed record TourSaveAsTemplateRequest(Localized<string> Title);

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
internal sealed partial class TourMapper
{
    // The state is not a column: it is handed in, computed by TourState, and mapped by name.
    [MapProperty(nameof(Tour.BriefingJson), nameof(TourDetailDto.Briefing))]
    private partial TourDetailDto ToDetail(Tour tour, TourStateKind state, bool isPublic);

    private partial TourListDto ToList(Tour tour, TourStateKind state);

    /// <summary>
    /// Everything the client may set, and nothing else: the template flag only on a new row, the briefing only when
    /// sent, the window only when sent. A template has no address and no dates (§1.10), and only a hub tour an order
    /// of its rotations.
    /// </summary>
    [MapperIgnoreSource(nameof(TourWriteDto.Briefing))]
    [MapperIgnoreSource(nameof(TourWriteDto.ReportWindowDays))]
    [MapperIgnoreSource(nameof(TourWriteDto.IsTemplate))]
    [MapperIgnoreSource(nameof(TourWriteDto.AllowedAircraft))]
    [MapperIgnoreSource(nameof(TourWriteDto.ParentTourId))]
    [MapperIgnoreSource(nameof(TourWriteDto.RequiredSubtours))]
    [MapperIgnoreTarget(nameof(Tour.ParentTourId))]
    [MapperIgnoreTarget(nameof(Tour.RequiredSubtours))]
    [MapperIgnoreTarget(nameof(Tour.AllowedAircraft))]
    [MapperIgnoreTarget(nameof(Tour.AllowedAircraftJson))]
    [MapperIgnoreTarget(nameof(Tour.BriefingJson))]
    [MapperIgnoreTarget(nameof(Tour.ReportWindowDays))]
    [MapperIgnoreTarget(nameof(Tour.IsTemplate))]
    private partial void ApplyFields(TourWriteDto payload, Tour tour);

    public TourDetailDto ToDetail(Tour tour, DateTime now) =>
        ToDetail(tour, TourState.Of(tour, now), TourState.IsPublic(tour, now));

    public TourListDto ToList(Tour tour, DateTime now) =>
        ToList(tour, TourState.Of(tour, now));

    public void Apply(TourWriteDto payload, Tour tour)
    {
        if (tour.Id == 0)
        {
            tour.IsTemplate = payload.IsTemplate;
            tour.ParentTourId = payload.IsTemplate ? null : payload.ParentTourId;
        }

        ApplyFields(payload, tour);
        tour.RequiredSubtours = tour.Kind == TourKind.Container ? payload.RequiredSubtours : null;

        // A subtour's empty date is its container's, copied before the permission is asked (TourSaving.AdoptAsync).
        tour.ReleaseFromParent = tour.IsSubtour && payload.ReleaseAt is null;
        tour.CloseFromParent = tour.IsSubtour && payload.CloseAt is null;
        tour.Slug = tour.IsTemplate ? null : payload.Slug?.Trim().ToLowerInvariant();
        tour.ReferenceAircraftIcao = string.IsNullOrWhiteSpace(payload.ReferenceAircraftIcao)
            ? null
            : payload.ReferenceAircraftIcao.Trim().ToUpperInvariant();
        tour.HubRotationOrder = tour.Kind == TourKind.Hub ? payload.HubRotationOrder ?? Tours.HubRotationOrder.Fixed : null;
        tour.RequiredNm = tour.Kind == TourKind.Distance ? payload.RequiredNm : null;
        tour.AllowedAircraft = AllowedAircraftCheck.Normalize(payload.AllowedAircraft);

        if (tour.IsTemplate)
        {
            tour.ReleaseAt = null;
            tour.CloseAt = null;
            tour.ShowPreview = false;
            tour.AwardId = null;
        }

        if (payload.Briefing is not null)
        {
            tour.BriefingJson = payload.Briefing.ToJsonString();
        }

        if (payload.ReportWindowDays is { } days)
        {
            tour.ReportWindowDays = days;
        }
    }

    /// <summary>An empty column is an empty document, never a null the renderer would trip on.</summary>
    private static JsonNode ParseBriefing(string json) => JsonNode.Parse(json) ?? JsonNode.Parse(Tour.EmptyBriefing)!;
}

/// <summary>The limits of a tour, shared by the validator and the columns.</summary>
public static partial class TourValidation
{
    public const int MaxSlugLength = 100;

    /// <summary>Twice around the world: a <c>Distance</c> tour asking for more is a typo.</summary>
    public const int MaxRequiredNm = 50_000;

    /// <summary>Types and groups together; a tour that admits more is a tour that admits all.</summary>
    public const int MaxAllowedAircraft = 100;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    public static partial Regex SlugPattern();
}

/// <summary>
/// The rules one payload can answer by itself. Messages are i18n keys. What needs other rows — a free address, a type
/// the hub knows, an award that exists, what a ready tour may still change — is <see cref="TourSaving"/>; what a tour
/// needs to be ready is <see cref="TourReadiness"/>.
/// </summary>
public sealed class TourWriteDtoValidator : AbstractValidator<TourWriteDto>
{
    public TourWriteDtoValidator(BlockDocumentWalker walker, BlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(walker);
        ArgumentNullException.ThrowIfNull(blocks);

        // A draft may be incomplete, but it has to be findable in a list: a name in at least one language.
        RuleFor(tour => tour.Title)
            .Must(title => title is not null && title.Values.Any(text => !string.IsNullOrWhiteSpace(text)))
            .WithMessage("errors.required");

        RuleFor(tour => tour.Kind).IsInEnum().WithMessage("errors.required");
        RuleFor(tour => tour.Progression).IsInEnum().WithMessage("errors.required");

        When(tour => !tour.IsTemplate, () =>
        {
            RuleFor(tour => tour.Slug)
                .NotEmpty().WithMessage("errors.required")
                .MaximumLength(TourValidation.MaxSlugLength).WithMessage("errors.text.tooLong")
                .Must(slug => slug is null || TourValidation.SlugPattern().IsMatch(slug.Trim().ToLowerInvariant()))
                .WithMessage("errors.slug.invalid");
        });

        // A subtour may leave its dates empty: they are then its container's.
        When(tour => !tour.IsTemplate && tour.ParentTourId is null, () =>
        {
            RuleFor(tour => tour.ReleaseAt).NotNull().WithMessage("errors.required");
            RuleFor(tour => tour.CloseAt).NotNull().WithMessage("errors.required");
        });

        RuleFor(tour => tour.CloseAt)
            .GreaterThan(tour => tour.ReleaseAt)
            .When(tour => !tour.IsTemplate && tour.ReleaseAt is not null && tour.CloseAt is not null)
            .WithMessage("flightops:errors.closeBeforeRelease");
        RuleFor(tour => tour.RequiredSubtours).InclusiveBetween(1, 100).When(tour => tour.RequiredSubtours is not null)
            .WithMessage("errors.number.range");

        RuleFor(tour => tour.ReportWindowDays).InclusiveBetween(1, 60).When(tour => tour.ReportWindowDays is not null)
            .WithMessage("errors.number.range");
        RuleFor(tour => tour.DailyLegLimit).InclusiveBetween(1, 100).When(tour => tour.DailyLegLimit is not null)
            .WithMessage("errors.number.range");
        RuleFor(tour => tour.MinPilotRating).InclusiveBetween(1, 20).When(tour => tour.MinPilotRating is not null)
            .WithMessage("errors.number.range");
        RuleFor(tour => tour.RequiredNm).InclusiveBetween(1, TourValidation.MaxRequiredNm).When(tour => tour.RequiredNm is not null)
            .WithMessage("errors.number.range");
        RuleFor(tour => tour.AllowedAircraft)
            .Must(allowed => allowed is null || (allowed.Types ?? []).Count + (allowed.GroupIds ?? []).Count <= TourValidation.MaxAllowedAircraft)
            .WithMessage("errors.text.tooLong");

        // The envelope of the briefing, and only the envelope, filed under the path of what is wrong.
        RuleFor(tour => tour.Briefing).Custom((briefing, context) =>
        {
            if (briefing is null)
            {
                return;
            }

            foreach (var error in walker.ValidateEnvelope(briefing, blocks.Types).Errors)
            {
                context.AddFailure(new FluentValidation.Results.ValidationFailure(
                    error.Path == "$" ? nameof(TourWriteDto.Briefing) : $"{nameof(TourWriteDto.Briefing)}.{error.Path}",
                    error.Key));
            }
        });
    }
}

public sealed class TourFromTemplateRequestValidator : AbstractValidator<TourFromTemplateRequest>
{
    public TourFromTemplateRequestValidator()
    {
        RuleFor(request => request.Title)
            .Must(title => title is not null && title.Values.Any(text => !string.IsNullOrWhiteSpace(text)))
            .WithMessage("errors.required");
        RuleFor(request => request.Slug)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(TourValidation.MaxSlugLength).WithMessage("errors.text.tooLong")
            .Must(slug => slug is null || TourValidation.SlugPattern().IsMatch(slug.Trim().ToLowerInvariant()))
            .WithMessage("errors.slug.invalid");
    }
}

public sealed class TourSaveAsTemplateRequestValidator : AbstractValidator<TourSaveAsTemplateRequest>
{
    public TourSaveAsTemplateRequestValidator()
    {
        RuleFor(request => request.Title)
            .Must(title => title is not null && title.Values.Any(text => !string.IsNullOrWhiteSpace(text)))
            .WithMessage("errors.required");
    }
}
