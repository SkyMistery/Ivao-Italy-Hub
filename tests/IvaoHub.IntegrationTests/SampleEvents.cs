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
            [],
            BannerMediaId is { } media ? [new MediaUseProjection(media, BannerNeededUntil)] : []);
    }
}
