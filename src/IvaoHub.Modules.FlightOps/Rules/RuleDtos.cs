using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentValidation;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.FlightOps.Shape;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.FlightOps.Rules;

/// <summary>A rule as the form loads it: its parameters as stored, its errors, whether it is retired.</summary>
public sealed record TourRuleDto(
    long Id,
    long? TourId,
    Department OwnerDepartment,
    string Code,
    Localized<string> Title,
    Localized<string> Text,
    long? AmendsRuleId,
    string? CheckKey,
    JsonNode Parameters,
    IReadOnlyList<long> ErrorIds,
    int Sort,
    bool Retired,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// A rule as the list shows it: the code of the rule it amends, the values of its parameters with their units, and how many
/// errors it has — its own and, on an amendment, its rule's — so that one with none stands out (design M2 §1.7). A list says
/// whether a row is in force (<c>Active</c>), not whether it is retired: its badge reads a true as "active".
/// </summary>
public sealed record TourRuleListDto(
    long Id,
    long? TourId,
    Department OwnerDepartment,
    string Code,
    Localized<string> Title,
    long? AmendsRuleId,
    string? AmendsCode,
    string? CheckKey,
    IReadOnlyList<string> Values,
    int Errors,
    int Sort,
    bool Active,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// What a client may set on a rule. The tour and the rule amended are chosen when it is created and never change; an
/// amendment's check is its rule's, whatever is sent.
/// </summary>
public sealed record TourRuleWriteDto(
    long? TourId,
    string Code,
    Localized<string> Title,
    Localized<string> Text,
    long? AmendsRuleId,
    string? CheckKey,
    JsonNode? Parameters,
    IReadOnlyList<long>? ErrorIds,
    int Sort,
    bool Retired,
    DateTime RowVersion);

/// <summary>A rule as it holds on a tour (§5.2): the row that says it, the rule it amends, the parameters in force.</summary>
public sealed record EffectiveRuleDto(
    long Id,
    long? TourId,
    string Code,
    Localized<string> Title,
    Localized<string> Text,
    long? AmendsRuleId,
    string? AmendsCode,
    string? CheckKey,
    JsonNode Parameters,
    IReadOnlyList<string> Values,
    IReadOnlyList<long> ErrorIds);

/// <summary>An error as the form loads it.</summary>
public sealed record TourErrorDto(
    long Id,
    Department OwnerDepartment,
    Localized<string> Name,
    Localized<string> Description,
    Localized<string> Examples,
    ErrorCategory Category,
    int? YearlyMax,
    string? CheckKey,
    bool IsPublic,
    bool Retired,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>An error as the list shows it, with how many rules still in force name it.</summary>
public sealed record TourErrorListDto(
    long Id,
    Department OwnerDepartment,
    Localized<string> Name,
    ErrorCategory Category,
    int? YearlyMax,
    string? CheckKey,
    bool IsPublic,
    bool Active,
    int Rules,
    DateTime UpdatedAt,
    DateTime RowVersion);

public sealed record TourErrorWriteDto(
    Localized<string> Name,
    Localized<string> Description,
    Localized<string>? Examples,
    ErrorCategory Category,
    int? YearlyMax,
    string? CheckKey,
    bool IsPublic,
    bool Retired,
    DateTime RowVersion);

/// <summary>"Copy the rules of another tour": which one.</summary>
public sealed record CopyRulesRequest(long SourceTourId);

/// <summary>What the copy did: how many rules it added, and the codes it left out because the tour already had them.</summary>
public sealed record CopyRulesResultDto(int Copied, IReadOnlyList<string> Skipped);

/// <summary>The limits of a rule and of an error, shared by the validators and the columns.</summary>
public static partial class RuleValidation
{
    public const int MaxCodeLength = 16;

    public const int MaxCheckKeyLength = 32;

    /// <summary>A rule is broken in a handful of ways; fifty errors on one is a catalogue pasted in by mistake.</summary>
    public const int MaxErrorsPerRule = 50;

    /// <summary>A warning counted beyond once a day is no longer a warning.</summary>
    public const int MaxYearly = 366;

    [GeneratedRegex("^[A-Za-z0-9._-]{1,16}$")]
    public static partial Regex CodePattern();

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static string? Key(string? key) => string.IsNullOrWhiteSpace(key) ? null : key.Trim();
}

public sealed class TourRuleWriteDtoValidator : AbstractValidator<TourRuleWriteDto>
{
    public TourRuleWriteDtoValidator(IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(division);

        RuleFor(rule => rule.Code)
            .Must(code => RuleValidation.CodePattern().IsMatch(RuleValidation.Normalize(code)))
            .WithMessage("flightops:errors.ruleCode");
        RuleFor(rule => rule.Title).Required(division.Value);
        RuleFor(rule => rule.Text).Required(division.Value);
        RuleFor(rule => rule.CheckKey)
            .Must(key => RuleValidation.Key(key) is null || CheckCatalog.Exists(RuleValidation.Key(key)))
            .WithMessage("flightops:errors.checkUnknown");
        RuleFor(rule => rule.ErrorIds)
            .Must(ids => ids is null || ids.Count <= RuleValidation.MaxErrorsPerRule)
            .WithMessage("errors.text.tooLong");
        RuleFor(rule => rule.Sort).InclusiveBetween(0, 999).WithMessage("errors.number.range");

        // Only a tour's rule amends, and it amends a general one: the save checks which.
        RuleFor(rule => rule.AmendsRuleId)
            .Null()
            .When(rule => rule.TourId is null)
            .WithMessage("flightops:errors.generalAmendsNothing");
    }
}

public sealed class TourErrorWriteDtoValidator : AbstractValidator<TourErrorWriteDto>
{
    public TourErrorWriteDtoValidator(IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(division);

        RuleFor(error => error.Name).Required(division.Value);
        RuleFor(error => error.Description).Required(division.Value);
        RuleFor(error => error.Category).IsInEnum().WithMessage("errors.required");

        // A maximum belongs to a warning, and a warning has one (note 2026-09-14-requisiti-dei-tour §2).
        RuleFor(error => error.YearlyMax)
            .NotNull().WithMessage("errors.required")
            .InclusiveBetween(1, RuleValidation.MaxYearly).WithMessage("errors.number.range")
            .When(error => error.Category == ErrorCategory.Warning);
        RuleFor(error => error.YearlyMax)
            .Null()
            .When(error => error.Category != ErrorCategory.Warning)
            .WithMessage("flightops:errors.yearlyMaxOnlyWarning");
        RuleFor(error => error.CheckKey)
            .Must(key => RuleValidation.Key(key) is null || CheckCatalog.Exists(RuleValidation.Key(key)))
            .WithMessage("flightops:errors.checkUnknown");
    }
}

/// <summary>Rules and errors to and from their payloads: by hand, because parameters and links are not columns.</summary>
internal static class RuleMapper
{
    public static TourRuleDto ToDto(TourRule rule) => new(
        rule.Id,
        rule.TourId,
        rule.OwnerDepartment,
        rule.Code,
        rule.Title,
        rule.Text,
        rule.AmendsRuleId,
        rule.CheckKey,
        OpenCatalog.Parse(rule.ParametersJson),
        [.. (rule.RequestedErrorIds ?? rule.ErrorLinks.Select(link => link.ErrorId)).Order()],
        rule.Sort,
        rule.RetiredAt is not null,
        rule.UpdatedAt,
        rule.RowVersion);

    public static TourRuleListDto ToList(TourRule rule, string? amendsCode, int errors) => new(
        rule.Id,
        rule.TourId,
        rule.OwnerDepartment,
        rule.Code,
        rule.Title,
        rule.AmendsRuleId,
        amendsCode,
        rule.CheckKey,
        Values(rule.CheckKey, OpenCatalog.Parse(rule.ParametersJson)),
        errors,
        rule.Sort,
        rule.RetiredAt is null,
        rule.UpdatedAt,
        rule.RowVersion);

    public static EffectiveRuleDto ToDto(EffectiveRule effective) => new(
        effective.Rule.Id,
        effective.Rule.TourId,
        effective.Rule.Code,
        effective.Rule.Title,
        effective.Rule.Text,
        effective.Amended?.Id,
        effective.Amended?.Code,
        effective.CheckKey,
        effective.Parameters,
        Values(effective.CheckKey, effective.Parameters),
        effective.ErrorIds);

    /// <summary>What a list shows of the parameters of a check: each value with its unit, as the constraints' (T7c).</summary>
    public static IReadOnlyList<string> Values(string? checkKey, JsonObject parameters) =>
        OpenCatalog.Describe(CheckCatalog.Fields(checkKey), parameters);

    /// <summary>
    /// The tour and the rule amended only on a new row; the parameters as they were sent, which the save reads, normalizes
    /// and checks against the check (<see cref="CheckCatalog.Read"/>); retiring stamps the moment once.
    /// </summary>
    public static void Apply(TourRuleWriteDto payload, TourRule rule, DateTime now)
    {
        if (rule.Id == 0)
        {
            rule.TourId = payload.TourId;
            rule.AmendsRuleId = payload.AmendsRuleId;
        }

        rule.Code = RuleValidation.Normalize(payload.Code);
        rule.Title = payload.Title;
        rule.Text = payload.Text;
        rule.CheckKey = RuleValidation.Key(payload.CheckKey);
        rule.ParametersJson = payload.Parameters?.ToJsonString() ?? "{}";
        rule.RequestedErrorIds = [.. (payload.ErrorIds ?? []).Distinct()];
        rule.Sort = payload.Sort;
        rule.RetiredAt = payload.Retired ? rule.RetiredAt ?? now : null;
    }

    public static TourErrorDto ToDto(TourError error) => new(
        error.Id,
        error.OwnerDepartment,
        error.Name,
        error.Description,
        error.Examples,
        error.Category,
        error.YearlyMax,
        error.CheckKey,
        error.IsPublic,
        error.RetiredAt is not null,
        error.UpdatedAt,
        error.RowVersion);

    public static TourErrorListDto ToList(TourError error, int rules) => new(
        error.Id,
        error.OwnerDepartment,
        error.Name,
        error.Category,
        error.YearlyMax,
        error.CheckKey,
        error.IsPublic,
        error.RetiredAt is null,
        rules,
        error.UpdatedAt,
        error.RowVersion);

    public static void Apply(TourErrorWriteDto payload, TourError error, DateTime now)
    {
        error.Name = payload.Name;
        error.Description = payload.Description;
        error.Examples = payload.Examples ?? Localized<string>.Empty;
        error.Category = payload.Category;
        error.YearlyMax = payload.Category == ErrorCategory.Warning ? payload.YearlyMax : null;
        error.CheckKey = RuleValidation.Key(payload.CheckKey);
        error.IsPublic = payload.IsPublic;
        error.RetiredAt = payload.Retired ? error.RetiredAt ?? now : null;
    }
}
