using System.Globalization;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.Events.Registrations;

/// <summary>
/// What the registrations of an event answered to one question, summed (design M4 §4-bis.2: «how many at dinner, how many
/// monitors»): how many answered; for a yes or no, how many of each; for a choice, how many chose each; for a quantity, the total.
/// Numbers only, by the keys of the question: what the staff read live, and what the statistics keep once the registrations are gone
/// (<see cref="EventStats.AnswerSumsJson"/>, E17).
/// </summary>
public sealed record QuestionSum(
    long QuestionId,
    int Answered,
    int Yes,
    int No,
    int Total,
    IReadOnlyDictionary<string, int> Choices);

/// <summary>The sums of the answers of an event's registrations, and their words.</summary>
public static class RegistrationSums
{
    /// <summary>The sums of every question, over the registrations given: an answer to a question the event no longer has is not counted.</summary>
    public static IReadOnlyList<QuestionSum> Of(IReadOnlyList<EventQuestion> questions, IReadOnlyList<EventRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(registrations);

        var answers = registrations.Select(RegistrationAnswers.Of).ToList();

        return
        [
            .. questions.Select(question =>
            {
                var given = answers
                    .Select(answer => answer[EventQuestion.AnswerKey(question.Id)])
                    .OfType<System.Text.Json.Nodes.JsonNode>()
                    .ToList();

                var chosen = question.Type switch
                {
                    QuestionType.Choice => given.Select(answer => answer.GetValue<string>()).ToList(),
                    QuestionType.MultiChoice => given.SelectMany(answer => answer.AsArray().Select(key => key!.GetValue<string>())).ToList(),
                    _ => [],
                };

                return new QuestionSum(
                    question.Id,
                    given.Count,
                    question.Type == QuestionType.YesNo ? given.Count(answer => answer.GetValue<bool>()) : 0,
                    question.Type == QuestionType.YesNo ? given.Count(answer => !answer.GetValue<bool>()) : 0,
                    question.Type == QuestionType.Quantity ? given.Sum(answer => answer.GetValue<int>()) : 0,
                    question.Choices.ToDictionary(choice => choice.Key, choice => chosen.Count(key => key == choice.Key), StringComparer.Ordinal));
            }),
        ];
    }

    public const string AnsweredKey = "events:registrations.sums.answered";

    public const string TotalKey = "events:registrations.sums.total";

    /// <summary>
    /// The sum of one question in words, in this language, one entry each: «Yes 12», «No 3»; «PC 4», «Monitor 6», in the order of the
    /// choices; «Total 23» of a quantity; and how many answered a question in words.
    /// </summary>
    public static IReadOnlyList<string> Words(EventQuestion question, QuestionSum sum, string locale, string fallback, LocaleCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(sum);
        ArgumentNullException.ThrowIfNull(catalog);

        string Count(string words, int count) => $"{words} {count.ToString(CultureInfo.InvariantCulture)}";

        return question.Type switch
        {
            QuestionType.YesNo =>
            [
                Count(catalog.Resolve(locale, RegistrationAnswers.YesKey), sum.Yes),
                Count(catalog.Resolve(locale, RegistrationAnswers.NoKey), sum.No),
            ],
            QuestionType.Choice or QuestionType.MultiChoice =>
            [
                .. question.Choices.Select(choice =>
                    Count(choice.Label.Resolve(locale, fallback) ?? choice.Key, sum.Choices.GetValueOrDefault(choice.Key))),
            ],
            QuestionType.Quantity => [Count(catalog.Resolve(locale, TotalKey), sum.Total)],
            _ => [Count(catalog.Resolve(locale, AnsweredKey), sum.Answered)],
        };
    }
}
