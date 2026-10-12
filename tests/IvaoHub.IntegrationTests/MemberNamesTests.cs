using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.People;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Events;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The one reader of names by VID (E10m, note 2026-10-11-un-solo-lettore-dei-nomi): what it answers by itself, the same
/// answers as the three copies the modules still have — the tours' <see cref="PirepReview"/>, the training's
/// <see cref="TrainingPeople"/> and the events' <see cref="EventsPeople"/> —, and one test for each way those copies
/// differ. The core's own copy, in the contact threads, moved onto it in the same phase, and <c>ContactThreadTests</c>
/// still reads the name it gave.
/// <para>The people are the contact threads' (670011–670014): that class names them again every time it runs. The names
/// they had are put back at the end all the same.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class MemberNamesTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int Named = 670011;
    private const int Unnamed = 670012;
    private const int Blanks = 670013;
    private const int FirstOnly = 670014;

    /// <summary>Nobody has this VID: whoever the hub does not know.</summary>
    private const int Unknown = int.MaxValue;

    /// <summary>A row the hub never writes — an erasure takes the user away, and a pseudonym is nobody's —, far below any pseudonym.</summary>
    private const int BelowOne = -2_000_000_000;

    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ANameIsTheFirstAndLastNameJoinedAndTrimmedAndWhoeverTheHubDoesNotKnowIsLeftOut()
    {
        var token = TestContext.Current.CancellationToken;

        await WithPeopleAsync(token, async hub =>
        {
            var names = await MemberNames.ReadAsync(hub, [Named, Blanks, FirstOnly, Unknown, Named], token);

            Assert.Equal(3, names.Count);
            Assert.Equal("Mario Rossi", names[Named]);
            Assert.Equal("Anna  Maria Bianchi", names[Blanks]);
            Assert.Equal("Luca", names[FirstOnly]);
            Assert.False(names.ContainsKey(Unknown));
        });
    }

    [Fact]
    public async Task TheReaderAnswersWhatTheToursTheTrainingAndTheEventsAnswer()
    {
        var token = TestContext.Current.CancellationToken;

        await WithPeopleAsync(token, async hub =>
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var tours = scope.ServiceProvider.GetRequiredService<PirepReview>();
            var training = scope.ServiceProvider.GetRequiredService<TrainingPeople>();
            var events = scope.ServiceProvider.GetRequiredService<EventsPeople>();

            int?[][] asked =
            [
                [Named, Unnamed, Blanks, FirstOnly, Unknown, null, Named],
                [Unnamed],
                [Unknown],
                [null],
                [],
            ];

            foreach (var vids in asked)
            {
                var core = await MemberNames.ReadAsync(hub, vids, token);
                var known = vids.OfType<int>().ToList();

                Assert.Equal(Sorted(await tours.NamesAsync(vids, token)), Sorted(core));
                Assert.Equal(Sorted(await training.NamesAsync(vids, token)), Sorted(core));
                Assert.Equal(Sorted(await events.NamesAsync(known, token)), Sorted(core));
                Assert.Equal(Sorted(await MemberNames.ReadAsync(hub, known, token)), Sorted(core));
            }
        });
    }

    /// <summary>
    /// A member whose first and last names are both empty is kept with an empty name, as every copy kept them: whether an
    /// empty name reads as none is each module's to say, and each still says it from the core's answer.
    /// </summary>
    [Fact]
    public async Task AnEmptyNameIsKeptAndEachModuleStillReadsItItsOwnWay()
    {
        var token = TestContext.Current.CancellationToken;

        await WithPeopleAsync(token, async hub =>
        {
            var names = await MemberNames.ReadAsync(hub, [Unnamed], token);

            Assert.Equal(string.Empty, names[Unnamed]);

            // The tours call it no name; the training's mail writes the VID alone; the events' page gets the empty name.
            Assert.Null(PirepReview.Member(Unnamed, names)!.Name);
            Assert.Equal("670012", TrainingPeople.Label(Unnamed, names));
            Assert.Equal(string.Empty, TrainingPeople.Member(Unnamed, names)!.Name);
            Assert.Equal(string.Empty, EventsPeople.Member(Unnamed, names).Name);
        });
    }

    /// <summary>
    /// The trim is .NET's, on the joined name, not the database's: MariaDB's <c>TRIM</c> takes only spaces, and the copies
    /// all trimmed after reading.
    /// </summary>
    [Fact]
    public async Task ABlankOtherThanASpaceAtTheEdgeOfANameIsTrimmedToo()
    {
        var token = TestContext.Current.CancellationToken;

        await WithPeopleAsync(token, async hub =>
        {
            var names = await MemberNames.ReadAsync(hub, [Blanks], token);

            Assert.Equal("Anna  Maria Bianchi", names[Blanks]);
        });
    }

    /// <summary>
    /// The first difference: the tours left out every VID below one before asking, the others asked the database for it.
    /// The answer is the same while the hub has no user below one, which it never writes; the reader answers like the tours
    /// even when such a row is there (the decision on #247: an erased person, a negative VID, has no name).
    /// </summary>
    [Fact]
    public async Task AVidBelowOneIsNeverNamedEvenWhenAUserRowHasIt()
    {
        var token = TestContext.Current.CancellationToken;

        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        Assert.False(await hub.Users.AnyAsync(row => row.Vid == BelowOne, token));
        var stray = new HubUser
        {
            Vid = BelowOne,
            FirstName = "Nobody",
            LastName = "Atall",
            SecurityStamp = SuperadminService.NewStamp(),
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        };
        hub.Users.Add(stray);
        await hub.SaveChangesAsync(token);

        try
        {
            Assert.Empty(await MemberNames.ReadAsync(hub, [BelowOne, 0], token));
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<PirepReview>().NamesAsync([BelowOne, 0], token));

            // What the training's and the events' copies answer until they move onto the reader.
            Assert.Equal("Nobody Atall", (await scope.ServiceProvider.GetRequiredService<TrainingPeople>().NamesAsync([BelowOne], token))[BelowOne]);
            Assert.Equal("Nobody Atall", (await scope.ServiceProvider.GetRequiredService<EventsPeople>().NamesAsync([BelowOne], token))[BelowOne]);
        }
        finally
        {
            hub.Users.Remove(stray);
            await hub.SaveChangesAsync(token);
        }
    }

    /// <summary>
    /// The second difference: the training and the events did not ask the database when nobody was asked about, the tours
    /// and the contact threads did, with an empty list. The answer is empty either way; the reader does not ask, and a
    /// context that can no longer answer proves it.
    /// </summary>
    [Fact]
    public async Task NobodyToAskAboutDoesNotReachTheDatabase()
    {
        var token = TestContext.Current.CancellationToken;

        HubDbContext hub;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        }

        Assert.Empty(await MemberNames.ReadAsync(hub, Array.Empty<int>(), token));
        Assert.Empty(await MemberNames.ReadAsync(hub, [null, null], token));
        Assert.Empty(await MemberNames.ReadAsync(hub, [BelowOne, -1, 0], token));

        // The same context, asked about somebody, does reach it: the test above is not empty by accident.
        await Assert.ThrowsAsync<ObjectDisposedException>(() => MemberNames.ReadAsync(hub, [Named], token));
    }

    /// <summary>The third difference: the tours and the training take the columns that may name nobody, and a missing VID is left out.</summary>
    [Fact]
    public async Task AMissingVidIsLeftOutLikeAnUnknownOne()
    {
        var token = TestContext.Current.CancellationToken;

        await WithPeopleAsync(token, async hub =>
        {
            var names = await MemberNames.ReadAsync(hub, [null, Named, null], token);

            Assert.Equal(Named, Assert.Single(names).Key);
        });
    }

    private static List<KeyValuePair<int, string>> Sorted(IReadOnlyDictionary<int, string> names) =>
        [.. names.OrderBy(pair => pair.Key)];

    /// <summary>Gives the four people the names the tests read, runs the test, and puts back the names they had.</summary>
    private async Task WithPeopleAsync(CancellationToken cancellationToken, Func<HubDbContext, Task> test)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        (int Vid, string First, string Last)[] people =
        [
            (Named, "Mario", "Rossi"),
            (Unnamed, string.Empty, string.Empty),
            (Blanks, "\tAnna  Maria", "Bianchi\t\u00A0"),
            (FirstOnly, "Luca", string.Empty),
        ];

        Assert.False(await hub.Users.AnyAsync(row => row.Vid == Unknown, cancellationToken));

        var had = new Dictionary<int, (string First, string Last)>();
        foreach (var (vid, first, last) in people)
        {
            var user = await hub.Users.FirstOrDefaultAsync(row => row.Vid == vid, cancellationToken);
            if (user is null)
            {
                user = new HubUser { Vid = vid, SecurityStamp = SuperadminService.NewStamp(), CreatedAt = clock.UtcNow };
                hub.Users.Add(user);
            }
            else
            {
                had[vid] = (user.FirstName, user.LastName);
            }

            user.FirstName = first;
            user.LastName = last;
            user.UpdatedAt = clock.UtcNow;
        }

        await hub.SaveChangesAsync(cancellationToken);

        try
        {
            await test(hub);
        }
        finally
        {
            foreach (var (vid, (first, last)) in had)
            {
                var user = await hub.Users.FirstAsync(row => row.Vid == vid, cancellationToken);
                user.FirstName = first;
                user.LastName = last;
            }

            await hub.SaveChangesAsync(cancellationToken);
        }
    }
}
