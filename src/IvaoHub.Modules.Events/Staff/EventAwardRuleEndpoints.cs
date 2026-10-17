using FluentValidation;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Reports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>A rule of award as its form shows it (design M4 §1.9): the award, the criterion, and the parameters the criterion takes.</summary>
public sealed record EventAwardRuleDto(
    long Id,
    long EventId,
    Department OwnerDepartment,
    long AwardId,
    ReportKind Kind,
    AwardCriterion Criterion,
    string? DepartureIcao,
    string? ArrivalIcao,
    int? Count,
    int? DistanceNm,
    int? Minutes,
    string? PositionType,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// A rule of award as the event's list shows it: the same, with the award's name from the core's catalogue — none when the award is gone. Data
/// only: the page writes the criterion and its parameters in words.
/// </summary>
public sealed record EventAwardRuleRowDto(
    long Id,
    long EventId,
    Department OwnerDepartment,
    long AwardId,
    Localized<string>? AwardName,
    ReportKind Kind,
    AwardCriterion Criterion,
    string? DepartureIcao,
    string? ArrivalIcao,
    int? Count,
    int? DistanceNm,
    int? Minutes,
    string? PositionType,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>An active award of the core's catalogue a rule may name: its id and its name.</summary>
public sealed record AwardChoiceDto(long Id, Localized<string> Name);

/// <summary>What a rule's form chooses from: the active awards, and the kinds of position of the division, as the core spells them.</summary>
public sealed record AwardRuleChoicesDto(IReadOnlyList<AwardChoiceDto> Awards, IReadOnlyList<string> PositionTypes);

/// <summary>
/// What a client may set on a rule of award. The event is chosen when it is created and never changes; the kind of report is the criterion's,
/// written by the server; a parameter the criterion does not take is emptied.
/// </summary>
public sealed record EventAwardRuleWriteDto(
    long EventId,
    long AwardId,
    AwardCriterion Criterion,
    string? DepartureIcao,
    string? ArrivalIcao,
    int? Count,
    int? DistanceNm,
    int? Minutes,
    string? PositionType,
    DateTime RowVersion);

/// <summary>The rules one payload answers by itself (<see cref="AwardRules.Check"/>); the award, the airports and the kind of position are the save's.</summary>
public sealed class EventAwardRuleWriteDtoValidator : AbstractValidator<EventAwardRuleWriteDto>
{
    public EventAwardRuleWriteDtoValidator()
    {
        RuleFor(rule => rule.EventId).GreaterThan(0).WithMessage("errors.required");
        RuleFor(rule => rule.AwardId).GreaterThan(0).WithMessage("errors.required");
        RuleFor(rule => rule.PositionType)
            .MaximumLength(EventAwardRule.MaxPositionTypeLength).WithMessage("errors.text.tooLong");
        RuleFor(rule => rule).Custom((rule, context) =>
        {
            var problems = AwardRules.Check(
                rule.Criterion, rule.DepartureIcao, rule.ArrivalIcao, rule.Count, rule.DistanceNm, rule.Minutes, new Refusals());
            foreach (var (field, keys) in problems.Errors)
            {
                foreach (var key in keys)
                {
                    context.AddFailure(field, key);
                }
            }
        });
    }
}

/// <summary>Rules to and from their payloads: the kind from the criterion, the codes upper case, what the criterion does not take emptied.</summary>
internal static class AwardRuleMapper
{
    public static EventAwardRuleDto ToDto(EventAwardRule rule) => new(
        rule.Id,
        rule.EventId,
        rule.OwnerDepartment,
        rule.AwardId,
        rule.Kind,
        rule.Criterion,
        rule.DepartureIcao,
        rule.ArrivalIcao,
        rule.Count,
        rule.DistanceNm,
        rule.Minutes,
        rule.PositionType,
        rule.UpdatedAt,
        rule.RowVersion);

    public static EventAwardRuleRowDto Row(EventAwardRule rule, Localized<string>? awardName) => new(
        rule.Id,
        rule.EventId,
        rule.OwnerDepartment,
        rule.AwardId,
        awardName,
        rule.Kind,
        rule.Criterion,
        rule.DepartureIcao,
        rule.ArrivalIcao,
        rule.Count,
        rule.DistanceNm,
        rule.Minutes,
        rule.PositionType,
        rule.UpdatedAt,
        rule.RowVersion);

    /// <summary>The event only on a new row: a rule never moves to another event.</summary>
    public static void Apply(EventAwardRuleWriteDto payload, EventAwardRule rule)
    {
        if (rule.Id == 0)
        {
            rule.EventId = payload.EventId;
        }

        var takes = AwardRules.ParametersOf(payload.Criterion);
        rule.AwardId = payload.AwardId;
        rule.Criterion = payload.Criterion;
        rule.Kind = AwardRules.KindOf(payload.Criterion);
        rule.DepartureIcao = takes.HasFlag(AwardParameters.Route) ? ReportRules.Code(payload.DepartureIcao) : null;
        rule.ArrivalIcao = takes.HasFlag(AwardParameters.Route) ? ReportRules.Code(payload.ArrivalIcao) : null;
        rule.Count = takes.HasFlag(AwardParameters.Count) ? payload.Count : null;
        rule.DistanceNm = takes.HasFlag(AwardParameters.Distance) ? payload.DistanceNm : null;
        rule.Minutes = takes.HasFlag(AwardParameters.Minutes) ? payload.Minutes : null;
        rule.PositionType = takes.HasFlag(AwardParameters.PositionType) ? ReportRules.Code(payload.PositionType) : null;
        rule.RowVersion = payload.RowVersion;
    }
}

/// <summary>
/// The rules of award of an event (design M4 §1.9, §6.1, §7.2, E14b; note 2026-10-17-le-regole-di-award): a resource of the CRUD engine
/// filtered by <c>filter[eventId]</c>, read with <c>Events.View</c> and written with <c>Events.Edit</c> on the event's care — the event's own
/// area, as its airports. The tab «Awards» of the event's page is a generated list, and each rule a generated form. No hand written verb.
/// </summary>
public static class EventAwardRuleEndpoints
{
    public const string Pattern = "/api/events/award-rules";

    public static IEndpointRouteBuilder MapEventAwardRuleEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapCrud<EventAwardRule, EventAwardRuleRowDto, EventAwardRuleDto, EventAwardRuleWriteDto>(Pattern, options =>
        {
            options.PermissionArea = EventsPermissions.Area;
            options.Name = "EventAwardRules";
            options.ReadPolicy = EventsPermissions.View;
            options.WritePolicy = EventsPermissions.Edit;
            options.ContextType = typeof(EventsDbContext);

            // In the order they were written, the order the tab shows them in.
            options.DefaultOrder = rule => rule.Id;
            options.Filterable.Add(nameof(EventAwardRule.EventId));

            options.ToList = rule => AwardRuleMapper.Row(rule, null);
            options.ToListPage = PageAsync;
            options.ToDetail = AwardRuleMapper.ToDto;
            options.Apply = AwardRuleMapper.Apply;
            options.BeforeAuthorize = async (rule, saving) =>
                (await saving.Services.GetRequiredService<EventChildren>().AdoptAsync(rule, saving.CancellationToken)).Refusal;
            options.BeforeSave = SaveAsync;
        });

        // What a rule's form chooses from: the active awards of the core's catalogue — whoever writes an event need not read the catalogue in
        // the back office of the awards — and the kinds of position the division's positions have, as the core spells them, once each.
        app.MapGet($"{Pattern}/choices", ChoicesAsync)
            .WithTags("EventAwardRules")
            .WithName("EventsAwardRuleChoices")
            .Produces<AwardRuleChoicesDto>()
            .RequireAuthorization(EventsPermissions.Edit);

        return app;
    }

    /// <summary>
    /// A rule belongs to an event that takes reports — not one in person —, which has at most <see cref="EventAwardRule.MaxPerEvent"/>; its
    /// award is in the core's catalogue and active, the airports of a route are airports the core knows, and a kind of position is one the
    /// division's positions have (reading 2).
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> SaveAsync(EventAwardRule rule, CrudSaving saving)
    {
        var database = (EventsDbContext)saving.Database;

        if (await saving.Services.GetRequiredService<EventChildren>().EventAsync(rule.EventId, saving.CancellationToken) is not { } parent)
        {
            return EventChildren.Refusal("eventId", "events:errors.eventUnknown");
        }

        if (parent.InPerson)
        {
            return EventChildren.Refusal("eventId", "events:errors.awardRuleInPerson");
        }

        if (saving.IsNew
            && await database.AwardRules.CountAsync(row => row.EventId == rule.EventId, saving.CancellationToken) >= EventAwardRule.MaxPerEvent)
        {
            return EventChildren.Refusal("eventId", AwardRules.TooManyKey);
        }

        var refusals = new Refusals();
        var hub = saving.Services.GetRequiredService<HubDbContext>();
        if (!await hub.Awards.AsNoTracking().AnyAsync(award => award.Id == rule.AwardId && award.IsActive, saving.CancellationToken))
        {
            refusals.Add("awardId", AwardRules.AwardUnknownKey);
        }

        if (rule is { DepartureIcao: { } departure, ArrivalIcao: { } arrival })
        {
            var found = await saving.Services.GetRequiredService<IAirportDirectory>().FindAsync([departure, arrival], saving.CancellationToken);
            if (!found.ContainsKey(departure))
            {
                refusals.Add("departureIcao", "events:errors.airportUnknown");
            }

            if (!found.ContainsKey(arrival))
            {
                refusals.Add("arrivalIcao", "events:errors.airportUnknown");
            }
        }

        if (rule.PositionType is { } type
            && !(await saving.Services.GetRequiredService<IAtcPositionDirectory>().OfDivisionAsync(saving.CancellationToken))
                .Any(position => string.Equals(position.Type, type, StringComparison.OrdinalIgnoreCase)))
        {
            refusals.Add("positionType", "events:errors.awardRulePositionType");
        }

        return refusals.IsEmpty ? null : refusals.Errors;
    }

    private static async Task<AwardRuleChoicesDto> ChoicesAsync(
        HubDbContext hub,
        IAtcPositionDirectory directory,
        CancellationToken cancellationToken)
    {
        var awards = await hub.Awards.AsNoTracking()
            .Where(award => award.IsActive)
            .OrderBy(award => award.Id)
            .Select(award => new AwardChoiceDto(award.Id, award.Name))
            .ToListAsync(cancellationToken);
        var types = (await directory.OfDivisionAsync(cancellationToken))
            .Select(position => position.Type.ToUpperInvariant())
            .Where(type => type.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new AwardRuleChoicesDto(awards, types);
    }

    /// <summary>A page of rules with the names of their awards, in one query to the core's catalogue.</summary>
    private static async Task<IReadOnlyList<EventAwardRuleRowDto>> PageAsync(
        IReadOnlyList<EventAwardRule> rules,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var ids = rules.Select(rule => rule.AwardId).Distinct().ToList();
        var names = await services.GetRequiredService<HubDbContext>().Awards.AsNoTracking()
            .Where(award => ids.Contains(award.Id))
            .ToDictionaryAsync(award => award.Id, award => award.Name, cancellationToken);

        return [.. rules.Select(rule => AwardRuleMapper.Row(rule, names.GetValueOrDefault(rule.AwardId)))];
    }
}
