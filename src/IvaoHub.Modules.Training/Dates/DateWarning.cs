using System.Text.Json;
using System.Text.Json.Serialization;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.Training.Dates;

/// <summary>What a warning of a date is about (design M3 §2.5). Stored by name.</summary>
public enum DateWarningKind
{
    /// <summary>Another training has its session on one of the days, whoever trains it.</summary>
    Training,

    /// <summary>An entry of the division's calendar, of a kind the division checks, touches one of the days.</summary>
    Calendar,
}

/// <summary>
/// One thing the hub found on the days a date touches (design M3 §2.5), as it was when the date was looked at: another training
/// with its session then — its ladder, rating and position, which the public calendar shows too, and never whose it is —, or an
/// entry of the calendar of one of the kinds of <c>conflictKinds</c>, with its title and its address. A date the trainer proposes
/// keeps its warnings (<see cref="TrainingSlot"/>), so nobody's name or VID is in them.
/// </summary>
/// <param name="Kind">A training, or an entry of the calendar.</param>
/// <param name="StartsAtUtc">When it starts: the session of the training, the entry.</param>
/// <param name="EndsAtUtc">When the entry ends; none for a session, and for an entry with no end.</param>
/// <param name="TrainingId">The other training, for the staff's page of it.</param>
/// <param name="TrainingKind">Its ladder.</param>
/// <param name="RatingShortName">Its rating, as the core's vocabulary names it.</param>
/// <param name="Position">Its position; none for a pilot's.</param>
/// <param name="CalendarKind">The kind of the entry, as the calendar names it.</param>
/// <param name="Title">The entry's title, in the languages of the division.</param>
/// <param name="Url">Where the entry is read, when it has an address.</param>
public sealed record DateWarning(
    DateWarningKind Kind,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    long? TrainingId,
    RatingKind? TrainingKind,
    string? RatingShortName,
    string? Position,
    string? CalendarKind,
    Localized<string>? Title,
    string? Url);

/// <summary>
/// The warnings of a date as a column holds them: JSON, with the names of the enums and every language of a title, and without
/// what a warning does not have — a title left out is read back as none, where a title written as <c>null</c> would come back
/// empty. The serializer of the requests is not the one a column is written with, so this one says what it needs itself
/// (<c>CONTRIBUTING.md</c>, «Traps»).
/// </summary>
public static class DateWarnings
{
    private static readonly JsonSerializerOptions Json = Build();

    public static string Write(IReadOnlyList<DateWarning> warnings) => JsonSerializer.Serialize(warnings, Json);

    public static IReadOnlyList<DateWarning> Read(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<DateWarning>>(json, Json) ?? [];

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new LocalizedJsonConverterFactory());
        return options;
    }
}
