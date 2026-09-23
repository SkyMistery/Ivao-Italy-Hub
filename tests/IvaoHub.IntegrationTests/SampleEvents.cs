using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A row of the test module that projects itself (M2, T4): what a tour or an event will be, with
/// nothing of either in it. Until T4 a row of a module did not project at all — its context did not
/// hold the projection tables and the interceptor skipped it without a word (note
/// 2026-09-15-contatti-con-risposte §3.3) — and nothing noticed, because no module projected yet.
/// This row is what notices now.
/// </summary>
public sealed class SampleEvent : IProjectable, IPublishable
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime StartsAt { get; set; }

    /// <summary>How many days it lasts: one calendar entry per day, as a tour has one per leg window.</summary>
    public int Days { get; set; } = 1;

    public PublishStatus Status { get; set; }

    public DateTime? PublishedAt { get; set; }

    /// <summary>A row that asks to disappear from every projection, as a deleted one would.</summary>
    public bool IsWithdrawn { get; set; }

    /// <summary>The picture it shows, and until when it needs it; no date means for as long as the row lives.</summary>
    public long? BannerMediaId { get; set; }

    public DateTime? BannerNeededUntil { get; set; }

    /// <summary>
    /// The member the row points out for an award, and the award it proposes (T4b): what a completed
    /// tour will signal. Not columns — the test keeps the instance and saves it again, and the
    /// projection reads the instance, not the table.
    /// </summary>
    public int? AwardeeVid { get; set; }

    public long? ProposedAwardId { get; set; }

    /// <summary>
    /// The member who contests it, and who else takes part (M2, T14): what a disputed report will project. Not columns,
    /// like the award above.
    /// </summary>
    public int? DisputedBy { get; set; }

    public int? DisputeParticipant { get; set; }

    public string SourceModule => SampleModule.ModuleKey;

    public string SourceId => $"event:{Id}";

    public ProjectionSnapshot? Project(ProjectionContext context)
    {
        if (IsWithdrawn)
        {
            return null;
        }

        var title = new Localized<string>(context.Locales.ToDictionary(locale => locale, _ => Title));
        var url = $"{SampleModule.NavigationPath}/{Id}";

        return new ProjectionSnapshot(
            new SearchProjection("sample", url, Department.ED, Visibility.Public, title, title),
            [
                .. Enumerable.Range(0, Days).Select(day => new CalendarProjection(
                    "event",
                    StartsAt.AddDays(day),
                    StartsAt.AddDays(day).AddHours(2),
                    AllDay: false,
                    Department.ED,
                    Visibility.Public,
                    url,
                    title,
                    Description: null)),
            ],
            AwardeeVid is { } awardee ? [new AwardSignalProjection(awardee, $"completed {Title}", ProposedAwardId)] : [],
            BannerMediaId is { } media ? [new MediaUseProjection(media, BannerNeededUntil)] : [],
            DisputedBy is { } sender
                ?
                [
                    new ThreadOpeningProjection(
                        ContactKinds.Dispute,
                        Department.ED,
                        $"Dispute: {Title}",
                        "I do not agree with this.",
                        sender,
                        DisputeParticipant is { } participant ? [participant] : [],
                        [new ThreadReferenceProjection(SampleModule.ModuleKey, SourceId, title)]),
                ]
                : []);
    }
}

/// <summary>
/// The references of the test module a thread may cite (M2, T14): <c>event:{id}</c> exists for any positive id, and
/// brings one participant along, as a report brings its validator; <c>hidden:…</c> exists but not for the caller, which
/// is the same answer as missing.
/// </summary>
public sealed class SampleReferenceResolver : IContactReferenceResolver
{
    /// <summary>Who a sample reference adds to a thread.</summary>
    public const int Participant = 670019;

    public string SourceModule => SampleModule.ModuleKey;

    public Task<ContactReferenceTarget?> ResolveAsync(string sourceId, CancellationToken cancellationToken) =>
        Task.FromResult(
            sourceId.StartsWith("event:", StringComparison.Ordinal)
                && long.TryParse(sourceId.AsSpan("event:".Length), out var id)
                && id > 0
                ? new ContactReferenceTarget(
                    new Localized<string>(new Dictionary<string, string> { ["en"] = $"Event {id}", ["it"] = $"Evento {id}" }),
                    $"{SampleModule.NavigationPath}/{id}",
                    [Participant])
                : null);
}
