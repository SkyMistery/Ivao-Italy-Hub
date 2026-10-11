using System.Text.Json.Nodes;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.Events.Registrations;

/// <summary>
/// What a member sends to register to an event in person, or to change their answers (design M4 §4-bis.2): the answers by question,
/// as the member's form holds them (<see cref="RegistrationAnswers"/>). The server checks each by its type.
/// </summary>
public sealed record RegistrationRequest(JsonObject? Answers);

/// <summary>
/// A registration as its member reads it, on <c>/events/mine</c> and on the event's page: the event, the answers as the form holds
/// them — to draw it again for a change — and in words of the member's language, and whether it can still be changed or withdrawn:
/// until the event starts (§4-bis.2).
/// </summary>
public sealed record MyRegistrationDto(
    long Id,
    long EventId,
    string EventSlug,
    Localized<string> EventTitle,
    EventStateKind EventState,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    Localized<string>? Venue,
    JsonObject Answers,
    IReadOnlyList<string> Lines,
    bool Changeable,
    bool Withdrawable,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// A registration as the staff of an event read it in the tab «Registrations» of its page (design M4 §7.2, E16): the member, named the
/// way the core names a person (<c>{ vid, name }</c>, so that an erased one is said with the core's word), their answers in words of
/// the reader's language, one line per question, when they registered and when they last changed their answers. The words are read in
/// the list, where a page reads its questions and its names in one query each; a row read alone has none.
/// </summary>
public sealed record EventRegistrationDto(
    long Id,
    long EventId,
    Department OwnerDepartment,
    EventMemberDto Member,
    IReadOnlyList<string> Answers,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>When a registration can be sent, changed or withdrawn (§4-bis.2): one place for the member's verbs and their page.</summary>
public static class RegistrationRules
{
    /// <summary>An event in person that does not take registrations, or no longer does.</summary>
    public const string ClosedKey = "events:errors.registrationClosed";

    /// <summary>
    /// Whether the member may register or change their answers now: an event in person, not cancelled, before its start. Seen by them
    /// is the endpoint's to ask.
    /// </summary>
    public static bool IsOpen(Event row, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.InPerson && row.CancelledAt is null && now < row.StartsAtUtc;
    }

    /// <summary>Whether the member may withdraw now: until the event starts, cancelled or not.</summary>
    public static bool IsWithdrawable(Event row, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(row);

        return now < row.StartsAtUtc;
    }
}
