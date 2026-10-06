using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The key of the initialisation marker (note 2026-09-28-il-marcatore-d-inizializzazione): every input of a step the mark
/// lets a start skip changes it, and nothing else does.
/// </summary>
public sealed class InitialisationKeyTests : IDisposable
{
    private static readonly BuildInfo Build = new("0.2.5", "0123456789abcdef0123456789abcdef01234567", DateTime.UnixEpoch, ".NET 10.0.0");
    private static readonly System.Reflection.Assembly[] Code = [typeof(InitialisationMarker).Assembly, typeof(InitialisationKeyTests).Assembly];
    private static readonly string[] Modules = ["flightops", "training"];

    private readonly string _seed = Path.Combine(Path.GetTempPath(), $"ivaohub-seed-{Guid.NewGuid():N}");

    public InitialisationKeyTests()
    {
        Directory.CreateDirectory(Path.Combine(_seed, "content-pages"));
        File.WriteAllText(Path.Combine(_seed, "content-pages", "home.json"), """{ "slug": "home" }""");
    }

    public void Dispose() => Directory.Delete(_seed, recursive: true);

    [Fact]
    public void TheSameStartGivesTheSameKeyAndNothingToDo()
    {
        var key = Key();

        Assert.Equal(key, Key());
        Assert.Empty(key.ChangesSince(Stored(key)));
        Assert.Equal("0.2.5+0123456", key.Stamp);
    }

    [Fact]
    public void AnotherBuildIsAFullInitialisation()
    {
        var before = Key();

        // Another number, another commit, one assembly more or less: each is another build.
        Assert.NotEqual(before.Build, Key(build: Build with { Version = "0.2.6" }).Build);
        Assert.NotEqual(before.Build, Key(build: Build with { Commit = "fedcba9876543210fedcba9876543210fedcba98" }).Build);
        Assert.NotEqual(before.Build, Key(code: [typeof(InitialisationMarker).Assembly]).Build);

        var after = Key(build: Build with { Version = "0.2.6" });
        Assert.Equal(before.Configuration, after.Configuration);
        Assert.Equal(before.Seed, after.Seed);
        Assert.Equal(["another build (the marker is of 0.2.5+0123456)"], after.ChangesSince(Stored(before)));
    }

    [Fact]
    public void TheOrderOfTheAssembliesIsNotAnotherBuild()
    {
        Assert.Equal(Key().Build, Key(code: [.. Code.Reverse(), Code[0]]).Build);
    }

    [Fact]
    public void EveryInputOfTheSkippedStepsInTheDivisionIsAFullInitialisation()
    {
        var before = Key();

        // The bootstrap list, the grants to positions, the languages the seed is resolved into, a module switched off.
        var changed = new[]
        {
            Key(division: Division() with { SuperAdmins = [704798, 111111] }),
            Key(division: Division() with
            {
                PositionGrants =
                [
                    new PositionGrantSeed { Department = Department.TD, Levels = [StaffLevel.Coordinator], Permission = "Training.View" },
                ],
            }),
            Key(division: Division() with { Locales = ["en"] }),
            Key(division: Division() with { Modules = new() { ["training"] = new ModuleSettings { Enabled = false } } }),
            Key(modules: ["flightops"]),
            Key(environment: "Development"),
        };

        foreach (var key in changed)
        {
            Assert.NotEqual(before.Configuration, key.Configuration);
            Assert.Equal(before.Build, key.Build);
            Assert.Equal(["the configuration changed"], key.ChangesSince(Stored(before)));
        }

        // The order the modules are listed in is not a change.
        Assert.Equal(before.Configuration, Key(modules: [.. Modules.Reverse()]).Configuration);
    }

    [Fact]
    public void AChangedAddedOrRenamedSeedFileIsAFullInitialisation()
    {
        var before = Key();

        File.WriteAllText(Path.Combine(_seed, "content-pages", "home.json"), """{ "slug": "home", "title": "new" }""");
        var edited = Key();
        Assert.NotEqual(before.Seed, edited.Seed);
        Assert.Equal(before.Configuration, edited.Configuration);
        Assert.Equal(["seed/ changed"], edited.ChangesSince(Stored(before)));

        Directory.CreateDirectory(Path.Combine(_seed, "calendar-kinds"));
        File.WriteAllText(Path.Combine(_seed, "calendar-kinds", "kinds.json"), "{}");
        var added = Key();
        Assert.NotEqual(edited.Seed, added.Seed);

        File.Move(Path.Combine(_seed, "calendar-kinds", "kinds.json"), Path.Combine(_seed, "calendar-kinds", "other.json"));
        Assert.NotEqual(added.Seed, Key().Seed);
    }

    [Fact]
    public void NoMarkerIsAFullInitialisationAndEveryChangeIsSaid()
    {
        var key = Key();
        Assert.Equal(["no marker"], key.ChangesSince(null));

        var other = Key(build: Build with { Version = "0.2.6" }, environment: "E2E");
        Assert.Equal(
            ["another build (the marker is of 0.2.5+0123456)", "the configuration changed"],
            other.ChangesSince(Stored(key)));
    }

    [Fact]
    public void TheOutcomeSaysWhichStepsWereSkippedOrWhyTheyRan()
    {
        var key = Key();
        var marker = Stored(key);

        Assert.Equal(
            "initialisation skipped (marker of 0.2.5+0123456, 2026-09-28 21:14:07Z): migrations, content",
            new InitialisationOutcome(true, [], marker).Describe(["migrations", "content"]));
        Assert.Equal(
            "initialisation full: no marker",
            new InitialisationOutcome(false, key.ChangesSince(null), null).Describe(["migrations"]));
    }

    [Fact]
    public void TheOutcomeSaysWhatTheLockDid()
    {
        // Note 2026-10-05-l-inizializzazione-sotto-blocco: held and initialised says nothing more (the wait is a step);
        // skipped behind another start says how long it waited; initialised without the lock says so, and why.
        var key = Key();
        var held = new InitialisationLockReport(Held: true, TimeSpan.FromMilliseconds(1234.4), Refusal: null);
        var refused = new InitialisationLockReport(Held: false, TimeSpan.FromSeconds(30), "not free after 30000 ms");

        Assert.Equal(
            "initialisation full: no marker",
            new InitialisationOutcome(false, key.ChangesSince(null), null) { Lock = held }.Describe(["migrations"]));
        Assert.Equal(
            "initialisation skipped (marker of 0.2.5+0123456, 2026-09-28 21:14:07Z): migrations, content; waited 1234 ms for the initialisation lock",
            new InitialisationOutcome(true, [], Stored(key)) { Lock = held }.Describe(["migrations", "content"]));
        Assert.Equal(
            "initialisation full: no marker; without the initialisation lock (not free after 30000 ms)",
            new InitialisationOutcome(false, key.ChangesSince(null), null) { Lock = refused }.Describe(["migrations"]));
    }

    [Fact]
    public void TheLockIsNamedAfterTheDatabaseAndNeverLongerThanTheDatabaseTakes()
    {
        // The server is shared and a lock's name is the server's: two installations must not wait for each other.
        Assert.Equal("hub-init:hub_one", InitialisationLock.NameFor("hub_one"));
        Assert.NotEqual(InitialisationLock.NameFor("hub_one"), InitialisationLock.NameFor("hub_two"));

        // A database's name may be as long as a lock's: then a hash of it, still its own.
        var longest = new string('a', 64);
        var other = new string('a', 63) + "b";
        Assert.Equal(InitialisationLock.LongestName, InitialisationLock.NameFor(longest).Length);
        Assert.StartsWith(InitialisationLock.NamePrefix, InitialisationLock.NameFor(longest), StringComparison.Ordinal);
        Assert.NotEqual(InitialisationLock.NameFor(longest), InitialisationLock.NameFor(other));
        Assert.Equal(InitialisationLock.LongestName, InitialisationLock.NameFor(new string('a', 55)).Length);
    }

    private InitialisationKey Key(
        BuildInfo? build = null,
        System.Reflection.Assembly[]? code = null,
        DivisionOptions? division = null,
        string[]? modules = null,
        string environment = "Production") =>
        InitialisationKey.Compute(build ?? Build, code ?? Code, division ?? Division(), modules ?? Modules, environment, _seed);

    private static DivisionOptions Division() => new()
    {
        Code = "XX",
        CountryId = "XX",
        Domain = "hub.example.org",
        Locales = ["en", "it"],
        DefaultLocale = "en",
        Timezone = "UTC",
        SuperAdmins = [704798],
    };

    private static StoredInitialisation Stored(InitialisationKey key) =>
        new(key.Build, key.Configuration, key.Seed, key.Stamp, new DateTime(2026, 9, 28, 21, 14, 7, DateTimeKind.Utc));
}
