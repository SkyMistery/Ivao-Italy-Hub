using System.Text.Json;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.Events;

/// <summary>
/// What a question of an event in person asks (design M4 §4-bis.1): a closed list, so that the server checks every answer by its
/// type and the staff read the sums (note 2026-09-29-gli-eventi-in-presenza §2.2). «Which equipment do you bring» is a
/// <see cref="MultiChoice"/>, or a <see cref="Quantity"/> per item; «are you coming to Saturday's dinner» a <see cref="YesNo"/>.
/// </summary>
public enum QuestionType
{
    YesNo,
    Choice,
    MultiChoice,
    Quantity,
    Text,
}

/// <summary>
/// One choice of a question: its key, which the server gives it once and the answers name, and its words in every language of the
/// division. The key never changes, so the staff rewrite and reorder the choices without touching an answer.
/// </summary>
public sealed record QuestionChoice(string Key, Localized<string> Label);

/// <summary>
/// A question of an event in person (design M4 §4-bis.1, E16), <c>evt_questions</c>: what the event asks whoever registers, in
/// the order the staff give, required or not. A row of the staff that belongs to its event (<see cref="IEventChild"/>), in the
/// area of the event itself (§6.1), with the event's care and scope copied at every write, as an airport of the event takes them.
/// <para>The member's form is built from these rows (§4-bis): each type is a field the form generator draws, and the server checks
/// each answer by the type (<c>Registrations.RegistrationAnswers</c>).</para>
/// </summary>
[Audited]
[PermissionArea(EventsPermissions.Area)]
public sealed class EventQuestion : IEventChild, IAuditable, IHasResourceScope
{
    /// <summary>The most questions of one event: a form of a page, not a survey.</summary>
    public const int MaxPerEvent = 30;

    /// <summary>The most choices of one question.</summary>
    public const int MaxChoices = 20;

    /// <summary>The longest question, and the longest choice, in each language: a sentence.</summary>
    public const int MaxLabelLength = 300;

    /// <summary>The highest place of a question in its event's form.</summary>
    public const int MaxOrdinal = 999;

    private static readonly JsonSerializerOptions ColumnJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LocalizedJsonConverterFactory() },
    };

    public long Id { get; set; }

    public long EventId { get; set; }

    /// <summary>Its place in the form: lowest first, then as written.</summary>
    public int Ordinal { get; set; }

    public QuestionType Type { get; set; }

    /// <summary>The question, in every language of the division: the members read it.</summary>
    public Localized<string> Label { get; set; } = Localized<string>.Empty;

    /// <summary>The choices of a <see cref="QuestionType.Choice"/> or <see cref="QuestionType.MultiChoice"/>, as a JSON array: the column.</summary>
    public string ChoicesJson { get; set; } = "[]";

    /// <summary>The choices, in the order the form offers them; none for the other types.</summary>
    public IReadOnlyList<QuestionChoice> Choices
    {
        get => JsonSerializer.Deserialize<List<QuestionChoice>>(ChoicesJson, ColumnJson) ?? [];
        set => ChoicesJson = JsonSerializer.Serialize(value ?? [], ColumnJson);
    }

    /// <summary>Whether a registration needs an answer to it. A yes or no always has one.</summary>
    public bool Required { get; set; }

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>The event's: a permission granted on one event reaches its questions.</summary>
    public string ResourceScope => Event.ScopeOf(EventId);

    /// <summary>Whether the type chooses among <see cref="Choices"/>.</summary>
    public static bool HasChoices(QuestionType type) => type is QuestionType.Choice or QuestionType.MultiChoice;

    /// <summary>The name of an answer to a question, in a registration and in the member's form: a letter first, so no form reads it as an index.</summary>
    public static string AnswerKey(long questionId) => $"q{questionId}";
}
