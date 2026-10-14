using System.Text.Json.Nodes;

namespace IvaoHub.Modules.Events.Registrations;

/// <summary>
/// What the registrations of an event answered to one question, summed (design M4 §4-bis.2: «how many at dinner, how many
/// monitors»): how many answered; for a yes or no, how many of each; for a choice, how many chose each, by the choice's key; for a
/// quantity, the total. Numbers only: the staff's list of the questions carries them and the page writes their words in the language
/// on screen (Carmine's answer 6 on #255); the statistics keep them once the registrations are gone (<see cref="EventStats.AnswerSumsJson"/>, E17).
/// </summary>
public sealed record QuestionSum(
    long QuestionId,
    int Answered,
    int Yes,
    int No,
    int Total,
    IReadOnlyDictionary<string, int> Choices);

/// <summary>The sums of the answers of an event's registrations.</summary>
public static class RegistrationSums
{
    /// <summary>
    /// The sums of every question, over the registrations given, each registration read once (the review of #255, point 4). An answer
    /// to a question the event no longer has is not counted, nor one that no longer fits its question (point 3).
    /// </summary>
    public static IReadOnlyDictionary<long, QuestionSum> Of(
        IReadOnlyList<EventQuestion> questions,
        IReadOnlyList<EventRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(registrations);

        var answered = questions.ToDictionary(question => question.Id, _ => 0);
        var yes = questions.ToDictionary(question => question.Id, _ => 0);
        var no = questions.ToDictionary(question => question.Id, _ => 0);
        var total = questions.ToDictionary(question => question.Id, _ => 0);
        var chosen = questions.ToDictionary(
            question => question.Id,
            question => question.Choices.ToDictionary(choice => choice.Key, _ => 0, StringComparer.Ordinal));

        foreach (var answers in registrations.Select(RegistrationAnswers.Of))
        {
            foreach (var question in questions)
            {
                var answer = answers[EventQuestion.AnswerKey(question.Id)];
                if (!RegistrationAnswers.Fits(question, answer))
                {
                    continue;
                }

                answered[question.Id]++;
                switch (question.Type)
                {
                    case QuestionType.YesNo when answer!.GetValue<bool>():
                        yes[question.Id]++;
                        break;
                    case QuestionType.YesNo:
                        no[question.Id]++;
                        break;
                    case QuestionType.Quantity:
                        total[question.Id] += answer!.GetValue<int>();
                        break;
                    case QuestionType.Choice:
                        Count(chosen[question.Id], answer!.GetValue<string>());
                        break;
                    case QuestionType.MultiChoice:
                        foreach (var key in answer!.AsArray().OfType<JsonNode>())
                        {
                            Count(chosen[question.Id], key.GetValue<string>());
                        }

                        break;
                }
            }
        }

        return questions.ToDictionary(
            question => question.Id,
            question => new QuestionSum(question.Id, answered[question.Id], yes[question.Id], no[question.Id], total[question.Id], chosen[question.Id]));
    }

    /// <summary>A choice the question still has is counted; a key it no longer has is not.</summary>
    private static void Count(Dictionary<string, int> chosen, string key)
    {
        if (chosen.TryGetValue(key, out var count))
        {
            chosen[key] = count + 1;
        }
    }

    /// <summary>The sums of a question nobody's registrations were read for: a row of the list read alone.</summary>
    public static QuestionSum None(EventQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        return new QuestionSum(question.Id, 0, 0, 0, 0, question.Choices.ToDictionary(choice => choice.Key, _ => 0, StringComparer.Ordinal));
    }
}
