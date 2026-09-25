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
/// <para>The shared database was seeded with <c>exam</c> when its first host started; each test puts it back the way a
/// release before A2 left it, and leaves it seeded again.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class CalendarKindSeedTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const string Exam = "exam";
    private const string ExamSetting = ContentSeeder.CalendarKindSettingPrefix + Exam;

    private static readonly string[] TheFiveBefore = ["event", "training", "tour", "meeting", "deadline"];

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
        await ForgetExamAsync(token);

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
        await ForgetExamAsync(token);

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
            await ForgetExamAsync(token);
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

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ContentSeeder>().SeedAsync(cancellationToken);
    }

    /// <summary>The installation as a release before A2 left it: no <c>exam</c>, and no key saying it was ever seeded.</summary>
    private async Task ForgetExamAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        database.CalendarKinds.RemoveRange(
            await database.CalendarKinds.Where(kind => kind.Key == Exam).ToListAsync(cancellationToken));
        database.DivisionSettings.RemoveRange(
            await database.DivisionSettings.Where(setting => setting.Key == ExamSetting).ToListAsync(cancellationToken));

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
