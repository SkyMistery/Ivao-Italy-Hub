using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.Events.Registrations;

/// <summary>
/// The answers of a registration (design M4 §4-bis.1, E16), checked by the server by the type of each question, written as the
/// column keeps them, and read back. One place for what an answer is, so that the member's form, the staff's list, the sums and the
/// mail read it alike.
/// <para>As the column and the member's form hold them, by question (<see cref="EventQuestion.AnswerKey"/>): a yes or no is
/// <c>true</c> or <c>false</c>; a choice the key of one; several choices the keys of each, in the order of the question; a quantity
/// a whole number; a text, trimmed. A question not answered is not there — an empty choice, no choice ticked, an empty text or an empty
/// quantity are no answer —, and the answers to questions the event no longer has are not kept.</para>
/// </summary>
public static class RegistrationAnswers
{
    /// <summary>Where every refusal of an answer lands: <c>answers.q12</c>, the field of the member's form.</summary>
    public const string Field = "answers";

    /// <summary>The longest text answer.</summary>
    public const int MaxTextLength = 1000;

    /// <summary>The highest quantity: of chairs, of monitors, of people at a table.</summary>
    public const int MaxQuantity = 999;

    /// <summary>
    /// The answers sent, checked against the questions of the event in their order: each by its type, each required one present. The
    /// refusals land on the field of the answer; when there are none, the answers as the column keeps them.
    /// </summary>
    public static JsonObject Check(IReadOnlyList<EventQuestion> questions, JsonObject? sent, Refusals problems)
    {
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(problems);

        var kept = new JsonObject();

        foreach (var question in questions)
        {
            var key = EventQuestion.AnswerKey(question.Id);
            var field = $"{Field}.{key}";
            var value = sent?[key];
            var (answer, refusal) = Read(question, value);

            if (refusal is not null)
            {
                problems.Add(field, refusal);
            }
            else if (answer is null)
            {
                if (question.Required)
                {
                    problems.Add(field, "errors.required");
                }
            }
            else
            {
                kept[key] = answer;
            }
        }

        return kept;
    }

    /// <summary>The answers a registration keeps, read from its column.</summary>
    public static JsonObject Of(EventRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return JsonNode.Parse(registration.AnswersJson) as JsonObject ?? [];
    }

    /// <summary>Whether a registration answered this question.</summary>
    public static bool Answered(EventRegistration registration, long questionId) =>
        Of(registration).ContainsKey(EventQuestion.AnswerKey(questionId));

    /// <summary>
    /// Whether a stored answer fits its question as it is now (the review of #255, point 3): a question whose type changed, in the
    /// instant between the staff's check and a member's change, may leave a value of the old type. Every reader skips such a value
    /// rather than fail on it.
    /// </summary>
    public static bool Fits(EventQuestion question, JsonNode? answer)
    {
        ArgumentNullException.ThrowIfNull(question);

        if (answer is null)
        {
            return false;
        }

        var kind = answer.GetValueKind();
        return question.Type switch
        {
            QuestionType.YesNo => kind is JsonValueKind.True or JsonValueKind.False,
            QuestionType.MultiChoice => kind == JsonValueKind.Array
                && answer.AsArray().All(item => item is not null && item.GetValueKind() == JsonValueKind.String),
            QuestionType.Quantity => kind == JsonValueKind.Number && answer.AsValue().TryGetValue<int>(out _),
            _ => kind == JsonValueKind.String,
        };
    }

    /// <summary>
    /// Whether a stored answer is one the sums count: it fits its question, and a choice — one or several — names only keys the question
    /// has. A text written while the question was a text, read after it became a choice, fits as a string and is still no choice (the
    /// second reading of #255, small 1): the erasure keeps only these.
    /// </summary>
    public static bool Counted(EventQuestion question, JsonNode? answer)
    {
        if (!Fits(question, answer))
        {
            return false;
        }

        var keys = question.Choices.Select(choice => choice.Key).ToHashSet(StringComparer.Ordinal);
        return question.Type switch
        {
            QuestionType.Choice => keys.Contains(answer!.GetValue<string>()),
            QuestionType.MultiChoice => answer!.AsArray().All(item => keys.Contains(item!.GetValue<string>())),
            _ => true,
        };
    }

    /// <summary>
    /// The answers in words, one line per question answered, in the order of the form: «the question: the answer», in this language.
    /// What the member reads in the mail (§8.3: «with a summary of the answers»), in the language of who receives it. The pages write
    /// their own words from the answers, in the language on screen (Carmine's answer 6 on #255).
    /// </summary>
    public static IReadOnlyList<string> Lines(
        IReadOnlyList<EventQuestion> questions,
        JsonObject answers,
        string locale,
        string fallback,
        LocaleCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(catalog);

        List<string> lines = [];

        foreach (var question in questions)
        {
            if (answers[EventQuestion.AnswerKey(question.Id)] is not { } answer || !Fits(question, answer))
            {
                continue;
            }

            var label = question.Label.Resolve(locale, fallback) ?? string.Empty;
            lines.Add($"{label}: {Words(question, answer, locale, fallback, catalog)}");
        }

        return lines;
    }

    /// <summary>The words of a choice: its label in this language, or its key for a choice the question no longer has.</summary>
    public static string ChoiceLabel(EventQuestion question, string key, string locale, string fallback)
    {
        ArgumentNullException.ThrowIfNull(question);

        return question.Choices.FirstOrDefault(choice => choice.Key == key)?.Label.Resolve(locale, fallback) ?? key;
    }

    public const string YesKey = "events:registrations.answers.yes";

    public const string NoKey = "events:registrations.answers.no";

    private static string Words(EventQuestion question, JsonNode answer, string locale, string fallback, LocaleCatalog catalog) =>
        question.Type switch
        {
            QuestionType.YesNo => catalog.Resolve(locale, answer.GetValue<bool>() ? YesKey : NoKey),
            QuestionType.Choice => ChoiceLabel(question, answer.GetValue<string>(), locale, fallback),
            QuestionType.MultiChoice => string.Join(", ", answer.AsArray().Select(key => ChoiceLabel(question, key!.GetValue<string>(), locale, fallback))),
            QuestionType.Quantity => answer.GetValue<int>().ToString(CultureInfo.InvariantCulture),
            _ => answer.GetValue<string>(),
        };

    /// <summary>One answer, by the type of its question: its value as the column keeps it, none, or the key of why it is refused.</summary>
    private static (JsonNode? Answer, string? Refusal) Read(EventQuestion question, JsonNode? value)
    {
        if (value is null)
        {
            return (null, null);
        }

        var kind = value.GetValueKind();

        switch (question.Type)
        {
            case QuestionType.YesNo:
                return kind is JsonValueKind.True or JsonValueKind.False
                    ? (JsonValue.Create(value.GetValue<bool>()), null)
                    : (null, "events:errors.answerYesNo");

            case QuestionType.Choice:
            {
                if (kind != JsonValueKind.String)
                {
                    return (null, "events:errors.answerChoice");
                }

                var key = value.GetValue<string>().Trim();
                return key.Length == 0 ? (null, null)
                    : question.Choices.Any(choice => choice.Key == key) ? (JsonValue.Create(key), null)
                    : (null, "events:errors.answerChoice");
            }

            case QuestionType.MultiChoice:
            {
                if (kind != JsonValueKind.Array
                    || value.AsArray().Any(item => item is null || item.GetValueKind() != JsonValueKind.String))
                {
                    return (null, "events:errors.answerChoice");
                }

                var keys = value.AsArray().Select(item => item!.GetValue<string>().Trim()).ToHashSet(StringComparer.Ordinal);
                if (keys.Any(key => !question.Choices.Any(choice => choice.Key == key)))
                {
                    return (null, "events:errors.answerChoice");
                }

                // In the order the question offers them: the same answer is written the same way whatever order it was ticked in.
                var chosen = question.Choices.Where(choice => keys.Contains(choice.Key)).Select(choice => (JsonNode?)JsonValue.Create(choice.Key));
                return keys.Count == 0 ? (null, null) : (new JsonArray([.. chosen]), null);
            }

            case QuestionType.Quantity:
                return kind == JsonValueKind.Number && value.AsValue().TryGetValue<int>(out var quantity) && quantity is >= 0 and <= MaxQuantity
                    ? (JsonValue.Create(quantity), null)
                    : (null, "events:errors.answerQuantity");

            default:
            {
                if (kind != JsonValueKind.String)
                {
                    return (null, "events:errors.answerText");
                }

                var text = value.GetValue<string>().Trim();
                return text.Length == 0 ? (null, null)
                    : text.Length > MaxTextLength ? (null, "errors.text.tooLong")
                    : (JsonValue.Create(text), null);
            }
        }
    }
}
