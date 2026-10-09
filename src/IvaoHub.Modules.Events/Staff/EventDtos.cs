using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FluentValidation;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.Events.Staff;

/// <summary>
/// An event as the staff's list shows it (design M4 §7.2): its state is read off its dates, never stored (§2.1), and its kind
/// is shown with the division's word for it, which the calendar keeps — null when the calendar no longer has that kind.
/// </summary>
public sealed record EventListDto(
    long Id,
    Department OwnerDepartment,
    string Slug,
    string Kind,
    Localized<string>? KindLabel,
    Localized<string> Title,
    EventStateKind State,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime UpdatedAt);

/// <summary>
/// An event as its page loads it (§1.2, §7.2). <c>State</c> is what the dates say now. The switches of the ATC roster and of
/// an event in person, the length of a shift and the limits of whoever books and does not fly are read here and written by
/// their own phases (M4b, M4c); who cancelled an event is the audit's to say.
/// </summary>
public sealed record EventDetailDto(
    long Id,
    Department OwnerDepartment,
    string Slug,
    string Kind,
    bool PublicSlots,
    bool PrivateSlots,
    bool HasRoster,
    bool WholeDivision,
    bool InPerson,
    EventOrganizer Organizer,
    string? ExternalUrl,
    Localized<string> Title,
    Localized<string> Summary,
    JsonNode Body,
    long? BannerMediaId,
    DateTime? VisibleFromUtc,
    DateTime? BookingOpensAtUtc,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    PublishStatus Status,
    DateTime? PublishedAt,
    Visibility Visibility,
    EventStateKind State,
    DateTime? CancelledAt,
    Localized<string>? CancellationNote,
    DateTime UpdatedAt,
    DateTime RowVersion);

/// <summary>
/// What a client may set on an event (§1.2, E3a). The status and the cancellation are not here: publishing is a verb of its
/// own (E3b), cancelling another (<see cref="EventCancelRequest"/>). Its department is the module's base department, which the
/// payload does not carry. Of the five switches, the three of M4a: the roster and the event in person enter with their phases.
/// A null <c>Body</c> keeps the description as it is.
/// </summary>
public sealed record EventWriteDto(
    string Kind,
    bool PublicSlots,
    bool PrivateSlots,
    bool WholeDivision,
    EventOrganizer Organizer,
    string? ExternalUrl,
    Localized<string> Title,
    string Slug,
    Localized<string> Summary,
    JsonNode? Body,
    long? BannerMediaId,
    DateTime? VisibleFromUtc,
    DateTime? BookingOpensAtUtc,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    Visibility Visibility,
    DateTime RowVersion);

/// <summary>"Cancel" (§2.3): why, in every language of the division — the page of the event shows it until its end —, and the version read.</summary>
public sealed record EventCancelRequest(Localized<string> Note, DateTime RowVersion);

/// <summary>"Publish" (§2.2, E3b): the version read; what the event needs to be published is <see cref="EventPublishing"/>'s.</summary>
public sealed record EventPublishRequest(DateTime RowVersion);

/// <summary>Events to and from their payloads: by hand, because the state is read off the clock and the description is a document.</summary>
internal static class EventMapper
{
    public static EventListDto ToList(Event row, DateTime now, Localized<string>? kindLabel) => new(
        row.Id,
        row.OwnerDepartment,
        row.Slug,
        row.Kind,
        kindLabel,
        row.Title,
        EventState.Of(row, now),
        row.StartsAtUtc,
        row.EndsAtUtc,
        row.UpdatedAt);

    public static EventDetailDto ToDetail(Event row, DateTime now) => new(
        row.Id,
        row.OwnerDepartment,
        row.Slug,
        row.Kind,
        row.PublicSlots,
        row.PrivateSlots,
        row.HasRoster,
        row.WholeDivision,
        row.InPerson,
        row.Organizer,
        row.ExternalUrl,
        row.Title,
        row.Summary,
        // An empty column is an empty document, never a null the editor would trip on.
        JsonNode.Parse(row.BodyJson) ?? JsonNode.Parse(Event.EmptyBody)!,
        row.BannerMediaId,
        row.VisibleFromUtc,
        row.BookingOpensAtUtc,
        row.StartsAtUtc,
        row.EndsAtUtc,
        row.Status,
        row.PublishedAt,
        row.Visibility,
        EventState.Of(row, now),
        row.CancelledAt,
        row.CancellationNote,
        row.UpdatedAt,
        row.RowVersion);

    /// <summary>
    /// Everything the payload says, and the version the caller edited, so a stale form is answered 409. The address in lower
    /// case and the kind as the calendar spells it, an empty link as none; the dates the validator found.
    /// </summary>
    public static void Apply(EventWriteDto payload, Event row)
    {
        row.Kind = payload.Kind.Trim();
        row.PublicSlots = payload.PublicSlots;
        row.PrivateSlots = payload.PrivateSlots;
        row.WholeDivision = payload.WholeDivision;
        row.Organizer = payload.Organizer;
        row.ExternalUrl = string.IsNullOrWhiteSpace(payload.ExternalUrl) ? null : payload.ExternalUrl.Trim();
        row.Title = payload.Title;
        row.Slug = payload.Slug.Trim().ToLowerInvariant();
        row.Summary = payload.Summary;
        row.BannerMediaId = payload.BannerMediaId;
        row.VisibleFromUtc = payload.VisibleFromUtc;
        row.BookingOpensAtUtc = payload.BookingOpensAtUtc;
        row.StartsAtUtc = payload.StartsAtUtc.GetValueOrDefault();
        row.EndsAtUtc = payload.EndsAtUtc.GetValueOrDefault();
        row.Visibility = payload.Visibility;
        row.RowVersion = payload.RowVersion;

        if (payload.Body is not null)
        {
            row.BodyJson = payload.Body.ToJsonString();
        }
    }
}

/// <summary>
/// The rules one payload can answer by itself (§1.2). Messages are i18n keys. A draft may be incomplete — what an event needs
/// to be published is <see cref="EventPublishing"/>'s —, but it has to be findable in a list, with an address, and its window
/// has to be one; what needs other rows — a free address, a kind of the calendar, a whole division without airports — is
/// <see cref="EventSaving"/>.
/// </summary>
public sealed partial class EventWriteDtoValidator : AbstractValidator<EventWriteDto>
{
    public EventWriteDtoValidator(BlockDocumentWalker walker, BlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(walker);
        ArgumentNullException.ThrowIfNull(blocks);

        RuleFor(row => row.Kind)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(CalendarKindWriteDtoValidator.MaxKeyLength).WithMessage("errors.text.tooLong");

        // A name in at least one language, to be found in a list; every language is asked when it is published (E3b).
        RuleFor(row => row.Title)
            .Must(title => title is not null && title.Values.Any(text => !string.IsNullOrWhiteSpace(text)))
            .WithMessage("errors.required");

        RuleFor(row => row.Slug)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(Event.MaxSlugLength).WithMessage("errors.text.tooLong")
            .Must(slug => slug is null || SlugPattern().IsMatch(slug.Trim().ToLowerInvariant()))
            .WithMessage("errors.slug.invalid");

        // The member's own page of the events is /events/mine (E6b, design §7.1): an event there would be a page nobody reaches.
        RuleFor(row => row.Slug)
            .Must(slug => !string.Equals(slug.Trim(), MinePage, StringComparison.OrdinalIgnoreCase))
            .When(row => !string.IsNullOrWhiteSpace(row.Slug))
            .WithMessage("events:errors.slugReserved");

        RuleFor(row => row.Organizer).IsInEnum().WithMessage("errors.required");

        // The page of whoever organises it leaves the site, so a browser has to follow it: the same rule as a link's.
        RuleFor(row => row.ExternalUrl)
            .MaximumLength(LinkWriteDtoValidator.MaxUrlLength).WithMessage("errors.text.tooLong")
            .Must(url => Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var parsed)
                && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
            .When(row => !string.IsNullOrWhiteSpace(row.ExternalUrl))
            .WithMessage("errors.url.absolute");

        RuleFor(row => row.StartsAtUtc).NotNull().WithMessage("errors.required");
        RuleFor(row => row.EndsAtUtc).NotNull().WithMessage("errors.required");
        RuleFor(row => row.EndsAtUtc)
            .GreaterThan(row => row.StartsAtUtc)
            .When(row => row.StartsAtUtc is not null && row.EndsAtUtc is not null)
            .WithMessage("events:errors.endsBeforeItStarts");

        // Read by everybody or by the members (§1.2): an event is never one department's.
        RuleFor(row => row.Visibility)
            .Must(visibility => visibility is Visibility.Public or Visibility.Members)
            .WithMessage("events:errors.visibilityChoice");

        // The envelope of the description, and only the envelope, filed under the path of what is wrong.
        RuleFor(row => row.Body).Custom((body, context) =>
        {
            if (body is null)
            {
                return;
            }

            foreach (var error in walker.ValidateEnvelope(body, blocks.Types).Errors)
            {
                context.AddFailure(new FluentValidation.Results.ValidationFailure(
                    error.Path == "$" ? nameof(EventWriteDto.Body) : $"{nameof(EventWriteDto.Body)}.{error.Path}",
                    error.Key));
            }
        });
    }

    /// <summary>The address under <c>/events/</c> of the member's own page, which no event takes.</summary>
    public const string MinePage = "mine";

    /// <summary>What an address of an event may hold: lower case letters, digits and single dashes, as every address of the hub.</summary>
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}

/// <summary>The note of a cancellation, in every language of the division: the page of the event says it to everybody until its end.</summary>
public sealed class EventCancelRequestValidator : AbstractValidator<EventCancelRequest>
{
    public EventCancelRequestValidator(IOptions<DivisionOptions> division)
    {
        ArgumentNullException.ThrowIfNull(division);

        RuleFor(request => request.Note).Required(division.Value);
    }
}
