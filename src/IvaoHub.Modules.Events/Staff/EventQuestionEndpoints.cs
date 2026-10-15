using System.Globalization;
using FluentValidation;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.Events.Data;
using IvaoHub.Modules.Events.Registrations;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>
/// A question of an event in person as its list and its form show it (design M4 §4-bis.1). <c>Sum</c> is what the registrations
/// answered to it, in numbers by the keys of the question (<see cref="RegistrationSums"/>), which the page writes in words in the
/// language on screen (Carmine's answer 6 on #255): read in the list, where a page sums its questions in one pass; all zero on a row
/// read alone.
/// </summary>
public sealed record EventQuestionDto(
    long Id,
    long EventId,
    Department OwnerDepartment,
    int Ordinal,
    QuestionType Type,
    Localized<string> Label,
    IReadOnlyList<QuestionChoice> Choices,
    bool Required,
    QuestionSum Sum,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// What a client may set on a question. The event is chosen when it is created and never changes; the care is the event's, taken
/// before the permission is asked. A choice with no key is new, and the server gives it one; a choice keeps its key for good.
/// </summary>
public sealed record EventQuestionWriteDto(
    long EventId,
    int Ordinal,
    QuestionType Type,
    Localized<string> Label,
    IReadOnlyList<QuestionChoiceWriteDto>? Choices,
    bool Required,
    DateTime RowVersion);

/// <summary>A choice as the form sends it: its key, empty for a new one, and its words.</summary>
public sealed record QuestionChoiceWriteDto(string? Key, Localized<string> Label);

/// <summary>
/// The rules one payload can answer by itself; whether its event is in person, and what the answers already given forbid, are the
/// save's. The members read a question and its choices, so they are written in every language of the division; a choice question
/// has at least two choices, and no other type has any. Messages are i18n keys.
/// </summary>
public sealed class EventQuestionWriteDtoValidator : AbstractValidator<EventQuestionWriteDto>
{
    public EventQuestionWriteDtoValidator(IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(division);

        RuleFor(question => question.EventId).GreaterThan(0).WithMessage("errors.required");
        RuleFor(question => question.Ordinal).InclusiveBetween(0, EventQuestion.MaxOrdinal).WithMessage("errors.number.range");
        RuleFor(question => question.Type).IsInEnum().WithMessage("errors.required");
        RuleFor(question => question.Label).Required(division.Value);
        RuleFor(question => question.Label)
            .Must(label => label is null || label.Values.All(text => (text?.Length ?? 0) <= EventQuestion.MaxLabelLength))
            .WithMessage("errors.text.tooLong");

        RuleFor(question => question.Choices)
            .Must(choices => choices is { Count: >= 2 and <= EventQuestion.MaxChoices })
            .When(question => EventQuestion.HasChoices(question.Type))
            .WithMessage("events:errors.questionChoicesCount");
        RuleFor(question => question.Choices)
            .Must(choices => choices is null or { Count: 0 })
            .When(question => !EventQuestion.HasChoices(question.Type))
            .WithMessage("events:errors.questionChoicesOnlyForChoice");

        RuleForEach(question => question.Choices).ChildRules(choice =>
        {
            choice.RuleFor(row => row.Label).Required(division.Value);
            choice.RuleFor(row => row.Label)
                .Must(label => label is null || label.Values.All(text => (text?.Length ?? 0) <= EventQuestion.MaxLabelLength))
                .WithMessage("errors.text.tooLong");
        });
    }
}

/// <summary>Questions to and from their payloads: by hand, because a new choice gets its key here.</summary>
internal static class QuestionMapper
{
    public static EventQuestionDto ToDto(EventQuestion question, QuestionSum sum) => new(
        question.Id,
        question.EventId,
        question.OwnerDepartment,
        question.Ordinal,
        question.Type,
        question.Label,
        question.Choices,
        question.Required,
        sum,
        question.UpdatedAt,
        question.RowVersion);

    /// <summary>
    /// The event only on a new row. Each choice keeps the key it has on the question; a choice without one, or with one the question
    /// never gave, is new and takes the next number, so the answers that name a key keep naming the same choice whatever is rewritten
    /// or reordered.
    /// </summary>
    public static void Apply(EventQuestionWriteDto payload, EventQuestion question)
    {
        if (question.Id == 0)
        {
            question.EventId = payload.EventId;
        }

        var given = question.Choices.Select(choice => choice.Key).ToHashSet(StringComparer.Ordinal);
        var next = given.Select(key => int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        question.Ordinal = payload.Ordinal;
        question.Type = payload.Type;
        question.Label = payload.Label;
        question.Required = payload.Required;
        question.Choices = EventQuestion.HasChoices(payload.Type)
            ? [
                .. (payload.Choices ?? []).Select(choice =>
                    new QuestionChoice(
                        choice.Key is { } key && given.Contains(key) && seen.Add(key)
                            ? choice.Key!
                            : (++next).ToString(CultureInfo.InvariantCulture),
                        choice.Label)),
            ]
            : [];
        question.RowVersion = payload.RowVersion;
    }
}

/// <summary>
/// The questions of an event in person (design M4 §4-bis, §7.2, E16): a resource of the CRUD engine filtered by
/// <c>filter[eventId]</c>, read with <c>Events.View</c> and written with <c>Events.Edit</c> on the event's care — the area of the
/// event itself (§6.1). The tab «Questions» of the event's page is a generated list with the sums of the answers — statistics of the
/// event, read with the event (§6.1: <c>Events.View</c> «and the statistics») —, and each question a generated form. No hand written
/// verb.
/// </summary>
public static class EventQuestionEndpoints
{
    public const string Pattern = "/api/events/questions";

    public static IEndpointRouteBuilder MapEventQuestionEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapCrud<EventQuestion, EventQuestionDto, EventQuestionDto, EventQuestionWriteDto>(Pattern, options =>
        {
            options.PermissionArea = EventsPermissions.Area;
            options.Name = "EventQuestions";
            options.ReadPolicy = EventsPermissions.View;
            options.WritePolicy = EventsPermissions.Edit;
            options.ContextType = typeof(EventsDbContext);

            // In the order of the form the members fill in.
            options.DefaultOrder = question => question.Ordinal;
            options.Sortable.Add(nameof(EventQuestion.Ordinal));
            options.Filterable.Add(nameof(EventQuestion.EventId));

            options.ToList = question => QuestionMapper.ToDto(question, RegistrationSums.None(question));
            options.ToListPage = PageAsync;
            options.ToDetail = question => QuestionMapper.ToDto(question, RegistrationSums.None(question));
            options.Apply = QuestionMapper.Apply;
            options.BeforeAuthorize = async (question, saving) =>
                (await saving.Services.GetRequiredService<EventChildren>().AdoptAsync(question, saving.CancellationToken)).Refusal;
            options.BeforeSave = SaveAsync;
        });

        return app;
    }

    /// <summary>
    /// A question belongs to an event in person, which has at most <see cref="EventQuestion.MaxPerEvent"/>. Once somebody answered it, it
    /// keeps its type and every choice it has — they are rewritten, reordered and added to, never taken away —, so no answer given
    /// stops meaning what it meant. Deleting it is allowed: its answers are no longer read, nor summed.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> SaveAsync(EventQuestion question, CrudSaving saving)
    {
        var database = (EventsDbContext)saving.Database;

        if (await saving.Services.GetRequiredService<EventChildren>().EventAsync(question.EventId, saving.CancellationToken) is not { } parent)
        {
            return EventChildren.Refusal("eventId", "events:errors.eventUnknown");
        }

        if (!parent.InPerson)
        {
            return EventChildren.Refusal("eventId", "events:errors.questionNotInPerson");
        }

        if (saving.IsNew
            && await database.Questions.CountAsync(row => row.EventId == question.EventId, saving.CancellationToken) >= EventQuestion.MaxPerEvent)
        {
            return EventChildren.Refusal("eventId", "events:errors.questionsTooMany");
        }

        if (saving.IsNew)
        {
            return null;
        }

        var registrations = await CrudSource.BackOffice<EventRegistration>(database).AsNoTracking()
            .Where(registration => registration.EventId == question.EventId)
            .ToListAsync(saving.CancellationToken);
        if (!registrations.Any(registration => RegistrationAnswers.Answered(registration, question.Id)))
        {
            return null;
        }

        var entry = database.Entry(question);
        var refusals = new Refusals();

        if (entry.Property(nameof(EventQuestion.Type)).OriginalValue is QuestionType before && before != question.Type)
        {
            refusals.Add("type", "events:errors.questionAnsweredType");
        }

        var keys = question.Choices.Select(choice => choice.Key).ToHashSet(StringComparer.Ordinal);
        var original = new EventQuestion { ChoicesJson = (string)entry.Property(nameof(EventQuestion.ChoicesJson)).OriginalValue! };
        if (original.Choices.Any(choice => !keys.Contains(choice.Key)))
        {
            refusals.Add("choices", "events:errors.questionAnsweredChoices");
        }

        return refusals.IsEmpty ? null : refusals.Errors;
    }

    /// <summary>
    /// A page of questions with their sums: the registrations of their events read once, each parsed once, and every question of an
    /// event summed in one pass (the review of #255, point 4).
    /// </summary>
    private static async Task<IReadOnlyList<EventQuestionDto>> PageAsync(
        IReadOnlyList<EventQuestion> questions,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<EventsDbContext>();
        var eventIds = questions.Select(question => question.EventId).Distinct().ToList();

        // An event past its retention has no registrations left: its sums are the statistics' (E17, reading 20 of its note).
        var retired = await services.GetRequiredService<InPersonRetention>().RetiredAsync(eventIds, cancellationToken);
        var live = eventIds.Where(id => !retired.ContainsKey(id)).ToList();
        var registrations = (await CrudSource.BackOffice<EventRegistration>(database).AsNoTracking()
                .Where(registration => live.Contains(registration.EventId))
                .ToListAsync(cancellationToken))
            .ToLookup(registration => registration.EventId);

        var sums = questions
            .GroupBy(question => question.EventId)
            .SelectMany(ofEvent => retired.TryGetValue(ofEvent.Key, out var stats)
                ? Kept(ofEvent, InPersonRetention.Sums(stats))
                : RegistrationSums.Of([.. ofEvent], [.. registrations[ofEvent.Key]]))
            .ToDictionary(sum => sum.Key, sum => sum.Value);

        return [.. questions.Select(question => QuestionMapper.ToDto(question, sums[question.Id]))];
    }

    /// <summary>The sums the statistics kept for these questions; zero for a question written after the registrations went.</summary>
    private static IReadOnlyDictionary<long, QuestionSum> Kept(IEnumerable<EventQuestion> questions, IReadOnlyDictionary<long, QuestionSum> kept) =>
        questions.ToDictionary(question => question.Id, question => kept.GetValueOrDefault(question.Id) ?? RegistrationSums.None(question));
}