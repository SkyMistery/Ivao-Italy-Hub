using IvaoHub.Core.Auth;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Events.Registrations;

/// <summary>
/// The registrations of an event as its staff list them (design M4 §7.2, E16): a page with the members' names and their answers in
/// words of the reader's language — one query for the names, one for the questions, never one per row.
/// </summary>
public sealed class StaffRegistrations(
    EventsDbContext database,
    EventsPeople people,
    LocaleCatalog catalog,
    IOptions<DivisionOptions> division,
    ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<EventRegistrationDto>> RowsAsync(
        IReadOnlyList<EventRegistration> registrations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var eventIds = registrations.Select(registration => registration.EventId).Distinct().ToList();
        var questions = (await database.Questions.AsNoTracking()
                .Where(question => eventIds.Contains(question.EventId))
                .OrderBy(question => question.Ordinal)
                .ThenBy(question => question.Id)
                .ToListAsync(cancellationToken))
            .ToLookup(question => question.EventId);
        var names = await people.NamesAsync(registrations.Select(registration => registration.Vid), cancellationToken);

        return
        [
            .. registrations.Select(registration => Row(
                registration,
                names,
                RegistrationAnswers.Lines(
                    [.. questions[registration.EventId]],
                    RegistrationAnswers.Of(registration),
                    currentUser.Locale,
                    division.Value.DefaultLocale,
                    catalog))),
        ];
    }

    /// <summary>One registration with its member's name and its answers in words; none known, a row read alone.</summary>
    public static EventRegistrationDto Row(EventRegistration registration, IReadOnlyDictionary<int, string> names, IReadOnlyList<string> answers)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return new EventRegistrationDto(
            registration.Id,
            registration.EventId,
            registration.OwnerDepartment,
            EventsPeople.Member(registration.Vid, names),
            answers,
            registration.CreatedAt,
            registration.UpdatedAt);
    }
}
