using System.Text.Json;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Modules.FlightOps;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Rules;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pure pieces of the validation (M2, T13a): the suggestion of design §4.3, the catalogue of notification types a module
/// adds to (note 2026-09-23-la-validazione §3.2), and the track kept compressed (§2.2).
/// </summary>
public sealed class ReviewTests
{
    private static readonly SnapshotErrorDto Dangerous = Error(1, ErrorCategory.Dangerous, yearlyMax: null);
    private static readonly SnapshotErrorDto Warning = Error(2, ErrorCategory.Warning, yearlyMax: 2);
    private static readonly SnapshotErrorDto Unlimited = Error(3, ErrorCategory.Warning, yearlyMax: null);
    private static readonly SnapshotErrorDto Info = Error(4, ErrorCategory.Info, yearlyMax: null);

    [Fact]
    public void NothingMarkedOrOnlyInformationIsAnAcceptance()
    {
        Assert.Equal(PirepStatus.Accepted, ReviewSuggestion.Of([], Counts()).Outcome);
        Assert.Equal(PirepStatus.Accepted, ReviewSuggestion.Of([Info, Unlimited], Counts((3, 40))).Outcome);
    }

    [Fact]
    public void ADangerousErrorRejectsFromItsFirstOccurrence()
    {
        var suggestion = ReviewSuggestion.Of([Info, Dangerous], Counts());

        Assert.Equal(PirepStatus.Rejected, suggestion.Outcome);
        Assert.Equal(ReviewSuggestion.Dangerous, Assert.Single(suggestion.Reasons).Reason);
    }

    /// <summary>With a maximum of two a year: the first and the second are accepted, the third is one over.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(7, true)]
    public void AWarningRejectsOnlyOnceItTakesTheYearOverItsMaximum(int alreadyThisYear, bool rejected)
    {
        var suggestion = ReviewSuggestion.Of([Warning], Counts((2, alreadyThisYear)));

        Assert.Equal(rejected ? PirepStatus.Rejected : PirepStatus.Accepted, suggestion.Outcome);
        if (rejected)
        {
            var reason = Assert.Single(suggestion.Reasons);
            Assert.Equal(ReviewSuggestion.OverYearlyMax, reason.Reason);
            Assert.Equal(alreadyThisYear + 1, reason.CountInYear);
            Assert.Equal(2, reason.YearlyMax);
        }
    }

    [Fact]
    public void ADecisionOverridesTheSuggestionOnlyWhenItAcceptsOrRejectsTheOtherWay()
    {
        var reject = ReviewSuggestion.Of([Dangerous], Counts());
        var accept = ReviewSuggestion.Of([], Counts());

        Assert.True(ReviewSuggestion.Overrides(PirepStatus.Accepted, reject));
        Assert.True(ReviewSuggestion.Overrides(PirepStatus.Rejected, accept));
        Assert.False(ReviewSuggestion.Overrides(PirepStatus.Rejected, reject));
        Assert.False(ReviewSuggestion.Overrides(PirepStatus.ToModify, reject));
        Assert.False(ReviewSuggestion.Overrides(PirepStatus.ToModify, accept));
    }

    [Fact]
    public void AModulesNotificationTypesAreNamedAfterItAndDeclaredOnce()
    {
        var catalog = new NotificationTypeCatalog([("tours", ["tours.accepted", "tours.digest"])]);

        Assert.Equal([.. NotificationTypes.All, "tours.accepted", "tours.digest"], catalog.All);
        Assert.True(catalog.IsKnown("tours.digest"));
        Assert.True(catalog.IsKnown(NotificationTypes.ContactReceived));
        Assert.False(catalog.IsKnown("tours.other"));

        Assert.Throws<InvalidOperationException>(() => new NotificationTypeCatalog([("tours", ["events.accepted"])]));
        Assert.Throws<InvalidOperationException>(() => new NotificationTypeCatalog([("tours", ["tours."])]));
        Assert.Throws<InvalidOperationException>(() => new NotificationTypeCatalog([("tours", ["tours.a", "tours.a"])]));
        Assert.Throws<InvalidOperationException>(() => new NotificationTypeCatalog([("contact", [NotificationTypes.ContactReceived])]));
    }

    [Fact]
    public void TheToursDeclareTheirNotificationTypesThroughTheModule()
    {
        var catalog = new NotificationTypeCatalog([(FlightOpsModule.ModuleKey, new FlightOpsModule().NotificationTypes)]);

        Assert.All(FlightOpsNotifications.All, type => Assert.True(catalog.IsKnown(type)));
    }

    /// <summary>A recorded track of about three hours: every point back, and a fraction of the JSON's size (note §2.2).</summary>
    [Fact]
    public void ATrackIsKeptCompressedAndReadBackWhole()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Fixture("tracker-tracks-62747397.json")));
        var points = IvaoTrackerReader.ReadTracks(document.RootElement);
        var raw = JsonSerializer.SerializeToUtf8Bytes(points);

        var stored = TrackCodec.Encode(points, DateTime.UtcNow);

        Assert.Equal(points.Count, stored.PointCount);
        Assert.True(stored.PointsGzip.Length * 4 < raw.Length, $"{stored.PointsGzip.Length} bytes stored for {raw.Length} of JSON.");
        Assert.Equal(points, TrackCodec.Decode(stored));
    }

    private static SnapshotErrorDto Error(long id, ErrorCategory category, int? yearlyMax) =>
        new(id, new Localized<string>(new Dictionary<string, string> { ["en"] = $"Error {id}" }), category, yearlyMax);

    private static Dictionary<long, int> Counts(params (long Error, int Count)[] counts) =>
        counts.ToDictionary(entry => entry.Error, entry => entry.Count);

    private static string Fixture(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IvaoHub.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "tests", "fixtures", "ivao", name);
    }
}
