using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The seed of the calendar's words, as a release that adds one meets an installation already running (M3, A2, note
/// <c>decisions/2026-09-25-le-postazioni-atc-e-il-tipo-exam.md</c>): <c>exam</c> arrives beside the five it already has,
/// which stay as they are, and an <c>exam</c> the back office wrote before the release is left alone — before A2 it made
/// the start fail on the unique index of the key.
/// <para>The same for the four words of the events (M4, E1, note
/// <c>decisions/2026-09-30-i-tipi-degli-eventi-e-l-ed-sul-banco.md</c>): they arrive beside the six a release before E1
/// left, and one of them the back office wrote first is left alone while the other three arrive.</para>
/// <para>The shared database was seeded with all of them when its first host started; each test puts it back the way a
/// release before A2 or before E1 left it, and leaves it seeded again.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class CalendarKindSeedTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const string Exam = "exam";
    private const string ExamSetting = ContentSeeder.CalendarKindSettingPrefix + Exam;

    private static readonly string[] TheFiveBefore = ["event", "training", "tour", "meeting", "deadline"];

    /// <summary>The words of the events, and the colour and the place each one is seeded with: next to <c>event</c>, in its colour.</summary>
    private static readonly (string Key, int Sort, string English, string Italian)[] TheEvents =
    [
        ("rfe", 11, "RFE", "RFE"),
        ("rfo", 12, "RFO", "RFO"),
        ("mse", 13, "MSE", "MSE"),
        ("online-day", 14, "Online Day", "Online Day"),
    ];

    private static readonly string[] EventKeys = [.. TheEvents.Select(kind => kind.Key)];

    private static readonly string[] TheSixBeforeE1 = [.. TheFiveBefore, Exam];

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ExamArrivesInAnInstallationThatAlreadyHasTheOtherFive()
    {
        var token = TestContext.Current.CancellationToken;
        await ForgetAsync([Exam], token);

        var before = await KindsAsync(token);
        Assert.DoesNotContain(Exam, before.Keys);
        Assert.All(TheFiveBefore, key => Assert.Contains(key, before.Keys));

        await SeedAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var exam = await database.CalendarKinds.AsNoTracking().SingleAsync(kind => kind.Key == Exam, token);
        Assert.Equal("red", exam.Colour);
        Assert.Equal(25, exam.Sort);
        Assert.True(exam.IsActive);
        Assert.Equal("Exam", exam.Label.Get("en"));
        Assert.Equal("Esame", exam.Label.Get("it"));
        Assert.True(await database.DivisionSettings.AnyAsync(setting => setting.Key == ExamSetting, token));

        // The five it had are the rows they were: the seed never writes a word twice.
        var after = await KindsAsync(token);
        Assert.All(TheFiveBefore, key => Assert.Equal(before[key], after[key]));
    }

    [Fact]
    public async Task AnExamWrittenByHandIsLeftAsItIsAndTheStartGoesOn()
    {
        var token = TestContext.Current.CancellationToken;
        await ForgetAsync([Exam], token);

        try
        {
            // The back office wrote the word first, in words and a colour of its own.
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
                database.CalendarKinds.Add(new CalendarKind
                {
                    Key = Exam,
                    Label = "Esame scritto a mano".L("An exam written by hand"),
                    Colour = "gray",
                    Sort = 90,
                });
                await database.SaveChangesAsync(token);
            }

            // What used to throw on the unique index, and take the start with it.
            await SeedAsync(token);
            await AssertTheHandWrittenOneStaysAsync(token);

            // Remembered: the next start does not even ask.
            await SeedAsync(token);
            await AssertTheHandWrittenOneStaysAsync(token);
        }
        finally
        {
            // Back to the seeded word, through the seed itself.
            await ForgetAsync([Exam], token);
            await SeedAsync(token);
        }
    }

    [Fact]
    public async Task TheWordsOfTheEventsArriveInAnInstallationThatAlreadyHasTheOtherSix()
    {
        var token = TestContext.Current.CancellationToken;
        await ForgetAsync(EventKeys, token);

        var before = await KindsAsync(token);
        Assert.All(EventKeys, key => Assert.DoesNotContain(key, before.Keys));
        Assert.All(TheSixBeforeE1, key => Assert.Contains(key, before.Keys));

        await SeedAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        foreach (var (key, sort, english, italian) in TheEvents)
        {
            var kind = await database.CalendarKinds.AsNoTracking().SingleAsync(row => row.Key == key, token);
            Assert.Equal("blue", kind.Colour);
            Assert.Equal(sort, kind.Sort);
            Assert.True(kind.IsActive);
            Assert.Equal(english, kind.Label.Get("en"));
            Assert.Equal(italian, kind.Label.Get("it"));

            var setting = ContentSeeder.CalendarKindSettingPrefix + key;
            Assert.True(await database.DivisionSettings.AnyAsync(row => row.Key == setting, token));
        }

        // The six it had are the rows they were.
        var after = await KindsAsync(token);
        Assert.All(TheSixBeforeE1, key => Assert.Equal(before[key], after[key]));
    }

    [Fact]
    public async Task AWordOfTheEventsWrittenByHandIsLeftAsItIsAndTheOthersArrive()
    {
        var token = TestContext.Current.CancellationToken;
        await ForgetAsync(EventKeys, token);

        try
        {
            // The events department had written its RFE in the back office before the release, in words and a colour of
            // its own — which is what a division already running is likely to have done.
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
                database.CalendarKinds.Add(new CalendarKind
                {
                    Key = "rfe",
                    Label = "RFE scritto a mano".L("An RFE written by hand"),
                    Colour = "pink",
                    Sort = 95,
                });
                await database.SaveChangesAsync(token);
            }

            await SeedAsync(token);
            await AssertTheHandWrittenRfeStaysAndTheOthersArriveAsync(token);

            // Remembered, all four: the next start does not even ask.
            await SeedAsync(token);
            await AssertTheHandWrittenRfeStaysAndTheOthersArriveAsync(token);
        }
        finally
        {
            // Back to the seeded words, through the seed itself.
            await ForgetAsync(EventKeys, token);
            await SeedAsync(token);
        }
    }

    private async Task AssertTheHandWrittenOneStaysAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var exam = Assert.Single(await database.CalendarKinds.AsNoTracking()
            .Where(kind => kind.Key == Exam)
            .ToListAsync(cancellationToken));

        Assert.Equal("gray", exam.Colour);
        Assert.Equal(90, exam.Sort);
        Assert.Equal("An exam written by hand", exam.Label.Get("en"));
        Assert.True(await database.DivisionSettings.AnyAsync(setting => setting.Key == ExamSetting, cancellationToken));
    }

    private async Task AssertTheHandWrittenRfeStaysAndTheOthersArriveAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var kinds = await database.CalendarKinds.AsNoTracking()
            .Where(kind => EventKeys.Contains(kind.Key))
            .ToListAsync(cancellationToken);

        // One row each: the hand-written RFE was neither doubled nor overwritten.
        Assert.Equal(EventKeys.Order(StringComparer.Ordinal), kinds.Select(kind => kind.Key).Order(StringComparer.Ordinal));

        var rfe = kinds.Single(kind => kind.Key == "rfe");
        Assert.Equal("pink", rfe.Colour);
        Assert.Equal(95, rfe.Sort);
        Assert.Equal("An RFE written by hand", rfe.Label.Get("en"));

        Assert.All(kinds.Where(kind => kind.Key != "rfe"), kind => Assert.Equal("blue", kind.Colour));

        foreach (var key in EventKeys)
        {
            var setting = ContentSeeder.CalendarKindSettingPrefix + key;
            Assert.True(await database.DivisionSettings.AnyAsync(row => row.Key == setting, cancellationToken));
        }
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ContentSeeder>().SeedAsync(cancellationToken);
    }

    /// <summary>
    /// The installation as a release before those words left it: none of them, and no key saying any was ever seeded.
    /// </summary>
    private async Task ForgetAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var settings = keys.Select(key => ContentSeeder.CalendarKindSettingPrefix + key).ToList();

        database.CalendarKinds.RemoveRange(
            await database.CalendarKinds.Where(kind => keys.Contains(kind.Key)).ToListAsync(cancellationToken));
        database.DivisionSettings.RemoveRange(
            await database.DivisionSettings.Where(setting => settings.Contains(setting.Key)).ToListAsync(cancellationToken));

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Each word by its key, with what would change if the seed wrote it again.</summary>
    private async Task<Dictionary<string, (long Id, DateTime UpdatedAt)>> KindsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        return await database.CalendarKinds.AsNoTracking()
            .ToDictionaryAsync(kind => kind.Key, kind => (kind.Id, kind.UpdatedAt), StringComparer.Ordinal, cancellationToken);
    }
}
