using System.Text.Json.Nodes;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Tours;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of T6 that need no database: the state of a tour read off its dates at each threshold (design M2
/// §1.2.1), what a tour projects and for whom, and what a template carries into a tour (§1.10).
/// </summary>
public sealed class TourStateTests
{
    private static readonly DateTime Release = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Close = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheStateFollowsTheDatesAtEveryThreshold()
    {
        var tour = ReadyTour();

        Assert.Equal(TourStateKind.Upcoming, TourState.Of(tour, Release.AddTicks(-1)));
        Assert.Equal(TourStateKind.Open, TourState.Of(tour, Release));
        Assert.Equal(TourStateKind.Open, TourState.Of(tour, Close));
        Assert.Equal(TourStateKind.Closing, TourState.Of(tour, Close.AddTicks(1)));
        Assert.Equal(TourStateKind.Closing, TourState.Of(tour, Close.AddDays(tour.ReportWindowDays)));
        Assert.Equal(TourStateKind.Closed, TourState.Of(tour, Close.AddDays(tour.ReportWindowDays).AddTicks(1)));

        tour.Status = PublishStatus.Draft;
        Assert.Equal(TourStateKind.Draft, TourState.Of(tour, Release.AddDays(3)));

        tour.IsTemplate = true;
        Assert.Equal(TourStateKind.Template, TourState.Of(tour, Release.AddDays(3)));
    }

    [Fact]
    public void ATourIsPublicFromItsReleaseOrItsPreviewAndNeverWhileHidden()
    {
        var tour = ReadyTour();

        Assert.False(TourState.IsPublic(tour, Release.AddDays(-1)));
        Assert.True(TourState.IsPublic(tour, Release));
        Assert.True(TourState.IsPublic(tour, Close.AddDays(60)));

        tour.ShowPreview = true;
        Assert.True(TourState.IsPublic(tour, Release.AddDays(-1)));

        tour.IsHidden = true;
        Assert.False(TourState.IsPublic(tour, Release.AddDays(1)));

        tour.IsHidden = false;
        tour.Status = PublishStatus.Draft;
        Assert.False(TourState.IsPublic(tour, Release.AddDays(1)));
    }

    [Fact]
    public void TheColumnOfVisibilityIsCoarseAndTheProjectionsFollowTheClock()
    {
        var tour = ReadyTour();
        tour.CoverMediaId = 7;
        tour.BannerMediaId = 8;

        // The column cannot follow the clock: public as soon as ready.
        Assert.Equal(Visibility.Public, tour.Visibility);

        var before = tour.Project(Context(Release.AddDays(-1)))!;
        Assert.Equal(Visibility.Staff, before.Search!.Visibility);
        Assert.All(before.Calendar, entry => Assert.Equal(Visibility.Staff, entry.Visibility));

        var after = tour.Project(Context(Release.AddMinutes(1)))!;
        Assert.Equal(Visibility.Public, after.Search!.Visibility);
        Assert.Equal("/tours/fo-test-state", after.Search.Url);
        Assert.Equal([Release, Close], after.Calendar.Select(entry => entry.StartsAtUtc));

        // The pictures stay a month after the close, and move with it.
        Assert.Equal([7L, 8L], after.MediaUses.Select(use => use.MediaId));
        Assert.All(after.MediaUses, use => Assert.Equal(Close + Tour.MediaKeptAfterClose, use.UsedUntilUtc));
    }

    [Fact]
    public void AHiddenTourAndATemplateProjectOnlyTheirPictures()
    {
        var tour = ReadyTour();
        tour.BannerMediaId = 8;
        tour.IsHidden = true;

        var hidden = tour.Project(Context(Release.AddDays(1)))!;
        Assert.Null(hidden.Search);
        Assert.Empty(hidden.Calendar);
        Assert.Equal([8L], hidden.MediaUses.Select(use => use.MediaId));

        tour.IsHidden = false;
        tour.IsTemplate = true;
        tour.ReleaseAt = null;
        tour.CloseAt = null;

        var template = tour.Project(Context(Release.AddDays(1)))!;
        Assert.Null(template.Search);
        Assert.Null(Assert.Single(template.MediaUses).UsedUntilUtc);

        tour.BannerMediaId = null;
        Assert.Null(tour.Project(Context(Release.AddDays(1))));
    }

    [Fact]
    public void ACopyCarriesTheSettingsAndNeitherTheDatesNorTheAddressNorTheAward()
    {
        var tour = ReadyTour();
        tour.Kind = TourKind.Hub;
        tour.HubRotationOrder = HubRotationOrder.Free;
        tour.DailyLegLimit = 4;
        tour.RequiresProcedures = true;
        tour.ReferenceAircraftIcao = "A320";
        tour.AwardId = 3;
        tour.IsHidden = true;
        tour.BannerMediaId = 8;
        tour.BriefingJson = """{"schemaVersion":1,"sections":[{"id":"s_original","blocks":[{"id":"b_original","type":"text","props":{}}]}]}""";

        var copy = TourCopy.Settings(tour, Walker());

        Assert.Equal(TourKind.Hub, copy.Kind);
        Assert.Equal(HubRotationOrder.Free, copy.HubRotationOrder);
        Assert.Equal(4, copy.DailyLegLimit);
        Assert.True(copy.RequiresProcedures);
        Assert.Equal("A320", copy.ReferenceAircraftIcao);
        Assert.Equal(8, copy.BannerMediaId);
        Assert.Equal(tour.ReportWindowDays, copy.ReportWindowDays);

        Assert.Null(copy.ReleaseAt);
        Assert.Null(copy.CloseAt);
        Assert.Null(copy.Slug);
        Assert.Null(copy.AwardId);
        Assert.False(copy.IsHidden);
        Assert.Equal(PublishStatus.Draft, copy.Status);

        // The briefing is a copy, not the same blocks under the same names.
        var section = JsonNode.Parse(copy.BriefingJson)!["sections"]![0]!;
        Assert.NotEqual("s_original", section["id"]!.GetValue<string>());
        Assert.NotEqual("b_original", section["blocks"]![0]!["id"]!.GetValue<string>());
    }

    private static Tour ReadyTour() => new()
    {
        Id = 42,
        Slug = "fo-test-state",
        Kind = TourKind.Sequential,
        Title = new Localized<string>(new Dictionary<string, string> { ["en"] = "State" }),
        Status = PublishStatus.Published,
        ReleaseAt = Release,
        CloseAt = Close,
        ReportWindowDays = 7,
        OwnerDepartment = Department.FOD,
    };

    private static BlockDocumentWalker Walker() => new(["en"]);

    private static ProjectionContext Context(DateTime now) => new(["en"], "en", Walker(), new StubClock(now));

    private sealed class StubClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }
}
