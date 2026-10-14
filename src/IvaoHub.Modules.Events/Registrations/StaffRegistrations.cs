namespace IvaoHub.Modules.Events.Registrations;

/// <summary>
/// The registrations of an event as its staff list them (design M4 §7.2, E16): a page with the members' names — one query, never one per
/// row — and their answers as they sent them, which the page writes in words in the language on screen (Carmine's answer 6 on #255).
/// </summary>
public sealed class StaffRegistrations(EventsPeople people)
{
    public async Task<IReadOnlyList<EventRegistrationDto>> RowsAsync(
        IReadOnlyList<EventRegistration> registrations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var names = await people.NamesAsync(registrations.Select(registration => registration.Vid), cancellationToken);

        return [.. registrations.Select(registration => Row(registration, names))];
    }

    /// <summary>One registration with its member's name — none known, a row read alone — and its answers.</summary>
    public static EventRegistrationDto Row(EventRegistration registration, IReadOnlyDictionary<int, string> names)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return new EventRegistrationDto(
            registration.Id,
            registration.EventId,
            registration.OwnerDepartment,
            EventsPeople.Member(registration.Vid, names),
            RegistrationAnswers.Of(registration),
            registration.CreatedAt,
            registration.UpdatedAt);
    }
}
