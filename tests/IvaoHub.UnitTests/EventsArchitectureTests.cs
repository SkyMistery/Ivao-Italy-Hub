using System.Text.Json;
using System.Text.RegularExpressions;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Events;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// What design M4 asks of the events and of no other module (§12, §13), so it lives here and not among the architecture tests
/// of the whole hub: the module does not name the network, writes no rating, no airport and no kind of event of its own — the
/// ratings and the airports are the core's to answer, the kinds are words of the division's calendar (note
/// 2026-09-29-i-tipi-di-evento) —, and it calls nobody outside the hub. And the two division files give the events department
/// and whoever collaborates on the events what the design says (§6.2, note 2026-09-29-chi-lavora-sugli-eventi).
/// <para>That no module names the archive of the ATC sessions nor its site is asked of every module already, the events
/// included, by <c>ArchitectureTests.NoModuleNamesTheAtcArchiveOrTheBoundaryDataset</c>: not written twice here.</para>
/// <para>They read the sources, as the architecture tests do, so they are patterns and not proofs: they catch the way these
/// rules get broken by habit — the network's word in a string, an airport written out, a number next to a rating — and the
/// review still reads the rest. The theories at the bottom show what each pattern catches and what it lets through.</para>
/// </summary>
public sealed partial class EventsArchitectureTests
{
    [Fact]
    public void TheModuleDoesNotNameTheNetwork()
    {
        var inTheCode = ServerSources().Concat(BrowserSources())
            .SelectMany(file => Lines(file, NamesTheNetwork));

        // The words: a language file may say what the network is to a member, but never where it lives.
        var inTheWords = LanguageFiles()
            .SelectMany(file => Lines(file, line => NetworkAddress().IsMatch(line)));

        Assert.Empty(inTheCode.Concat(inTheWords));
    }

    [Fact]
    public void TheModuleWritesNoRatingNoAirportAndNoKindOfEventOfItsOwn()
    {
        var words = NetworkWords();
        var kinds = SeededKinds();

        var offenders = ServerSources().Concat(BrowserSources())
            .SelectMany(file => Lines(file, line =>
            {
                var code = WithoutComment(line);
                return WritesARating(code, words) || WritesAnAirport(code) || WritesAKind(code, kinds);
            }));

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheModuleCallsNobodyOutsideTheHub()
    {
        // The core has the one client of the network (CLAUDE.md §2); the module asks the core, and has no client of its own.
        var offenders = ServerSources().SelectMany(file => Lines(file, line => AClientOfItsOwn().IsMatch(line)));

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheDivisionFilesGiveTheEventsAndWhoeverCollaboratesWhatTheDesignSays()
    {
        StaffLevel[] staff = [StaffLevel.Coordinator, StaffLevel.Assistant, StaffLevel.Advisor];
        StaffLevel[] heads = [StaffLevel.Coordinator, StaffLevel.Assistant];

        // Design §6.2: the coordinator and the assistant of the events department everything but validating a report of
        // support, which is whoever has that task (§5.3); its advisors (EA1–9) neither delete nor manage the settings.
        var design = new Dictionary<(string Subject, string Permission), StaffLevel[]>
        {
            [(nameof(Department.ED), EventsPermissions.View)] = staff,
            [(nameof(Department.ED), EventsPermissions.Edit)] = staff,
            [(nameof(Department.ED), EventsPermissions.Delete)] = heads,
            [(nameof(Department.ED), EventsPermissions.ManageSettings)] = heads,
            [(nameof(Department.ED), EventsPermissions.RoutesView)] = staff,
            [(nameof(Department.ED), EventsPermissions.RoutesEdit)] = staff,
            [(nameof(Department.ED), EventsPermissions.BookingsView)] = staff,
            [(nameof(Department.ED), EventsPermissions.BookingsEdit)] = staff,
            [(nameof(Department.ED), EventsPermissions.AtcView)] = staff,
            [(nameof(Department.ED), EventsPermissions.AtcEdit)] = staff,
            [(nameof(Department.ED), EventsPermissions.ReportsView)] = staff,

            // And the staff of a FIR (chief, assistant chief, advisor) on the ATC of its own FIR: a grant to the team of a FIR,
            // which takes effect with the first row of the area that says its FIR (E11a).
            [(FirTeam, EventsPermissions.AtcView)] = staff,
            [(FirTeam, EventsPermissions.AtcEdit)] = staff,
        };

        // ⚠️ Whoever collaborates — the ATC operations the ATC, the flight operations the routes, the membership the reports of
        // support, at every level and on every event (§6.2, §17.2 n.2) — waits for the core phase E2b (note
        // 2026-09-30-i-grant-di-chi-collabora-sugli-eventi, answer (b) of the maintainer on #209): until then a grant held on the
        // events department puts a position of another department inside it, for everything it sees, which is more than its part.
        // A grant of the seed is applied once and never taken back by the file, so none of these goes in before E2b; then they
        // move into the table above.
        (string Subject, string Permission)[] waiting =
        [
            (nameof(Department.AOD), EventsPermissions.View),
            (nameof(Department.AOD), EventsPermissions.AtcView),
            (nameof(Department.AOD), EventsPermissions.AtcEdit),
            (nameof(Department.FOD), EventsPermissions.View),
            (nameof(Department.FOD), EventsPermissions.RoutesView),
            (nameof(Department.FOD), EventsPermissions.RoutesEdit),
            (nameof(Department.MD), EventsPermissions.View),
            (nameof(Department.MD), EventsPermissions.ReportsView),
            (nameof(Department.MD), EventsPermissions.ReportsEdit),
        ];
        Assert.Empty(waiting.Intersect(design.Keys));

        foreach (var file in new[] { "division.json", "division.example.json" })
        {
            using var division = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "config", file)));
            var root = division.RootElement;

            Assert.Equal(
                nameof(Department.ED),
                root.GetProperty("modules").GetProperty(EventsModule.ModuleKey).GetProperty("baseDepartment").GetString());

            var grants = root.GetProperty("positionGrants").EnumerateArray()
                .Where(grant => EventsPermissions.All.Any(permission => permission.Name == grant.GetProperty("permission").GetString()))
                .ToList();

            // Once each, held on the events department: whoever collaborates reaches every event because the events department
            // is in every one (§1.1), never through the "in the care of".
            Assert.Equal(
                design.Keys.Select(key => $"{key.Subject} {key.Permission}").Order(StringComparer.Ordinal),
                grants.Select(grant => $"{Subject(grant)} {grant.GetProperty("permission").GetString()}").Order(StringComparer.Ordinal));

            foreach (var grant in grants)
            {
                var key = (Subject(grant), grant.GetProperty("permission").GetString()!);
                Assert.Equal(nameof(Department.ED), grant.GetProperty("scope").GetString());
                Assert.Equal(
                    design[key].Order(),
                    grant.GetProperty("levels").EnumerateArray().Select(level => Enum.Parse<StaffLevel>(level.GetString()!)).Order());
            }

            // Nobody who collaborates deletes an event nor manages the settings (§6.3): only the events department holds them.
            Assert.All(
                grants.Where(grant => grant.GetProperty("permission").GetString() is EventsPermissions.Delete or EventsPermissions.ManageSettings),
                grant => Assert.Equal(nameof(Department.ED), Subject(grant)));
        }
    }

    /// <summary>How the division files write a grant to the team of a FIR, with no department.</summary>
    private const string FirTeam = "firTeam";

    private static string Subject(JsonElement grant) =>
        grant.TryGetProperty("firTeam", out var team) && team.ValueKind == JsonValueKind.True
            ? FirTeam
            : grant.GetProperty("department").GetString()!;

    [Theory]
    [InlineData("using IvaoHub.Core.Ivao;", false)]
    [InlineData("namespace IvaoHub.Modules.Events.Settings;", false)]
    [InlineData("\"Server=localhost;Database=ivaohub;User ID=ivaohub\"", false)]
    [InlineData("import { Button } from '@ivao/atmosphere-react';", false)]
    [InlineData("private readonly IIvaoApiClient _client;", true)]
    [InlineData("/// <summary>The events of IVAO headquarters.</summary>", true)]
    [InlineData("const page = 'https://events.ivao.aero';", true)]
    public void TheNetworkIsNamedWhereThePatternSays(string line, bool named) => Assert.Equal(named, NamesTheNetwork(line));

    [Theory]
    [InlineData("var airport = \"LIRF\";", true)]
    [InlineData("const firstAirport = 'LIMC';", true)]
    [InlineData("if (position.Fir == \"LIRR\")", true)]
    [InlineData("airport.Property(row => row.Icao).HasMaxLength(4).IsRequired();", false)]
    [InlineData("entity.ToTable(\"evt_events\");", false)]
    [InlineData("const icao = 'lirf';", false)]
    [InlineData("\"events:errors.kindTwice\"", false)]
    public void AnAirportIsWrittenWhereThePatternSays(string line, bool written) => Assert.Equal(written, WritesAnAirport(line));

    [Theory]
    [InlineData("var preset = presets.First(row => row.Kind == \"rfe\");", true)]
    [InlineData("if (kind === 'online-day')", true)]
    [InlineData("new CalendarProjection(\"event\", starts, ends)", true)]
    [InlineData("public const string ModuleKey = \"events\";", false)]
    [InlineData("return $\"{EventsModule.ModuleKey}:event:{eventId}\";", false)]
    [InlineData("kind: z.string().min(1).meta({ choices: choices.kinds }),", false)]
    public void AKindIsWrittenWhereThePatternSays(string line, bool written) => Assert.Equal(written, WritesAKind(line, SeededKinds()));

    [Theory]
    [InlineData("if (controller.RatingAtc >= 5)", true)]
    [InlineData("var minimum = vocabulary.Find(RatingKind.Atc, 4);", true)]
    [InlineData("if (rating.ShortName == \"AS3\")", true)]
    [InlineData("var tower = positions.Where(position => position.Callsign.EndsWith(\"_TWR\"));", true)]
    [InlineData("var minimum = settings.MinimumAtcRating;", false)]
    [InlineData("var bookingGapMinutes = 10;", false)]
    public void ARatingIsWrittenWhereThePatternSays(string line, bool written) => Assert.Equal(written, WritesARating(line, NetworkWords()));

    // ---- the patterns ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The network's name anywhere, except inside the product's own (<c>IvaoHub</c>, and <c>ivaohub</c> for its database),
    /// the core's perimeter of the network (<c>IvaoHub.Core.Ivao</c>) and the package of the design system.
    /// </summary>
    private static bool NamesTheNetwork(string line) =>
        line.Replace("IvaoHub.Core.Ivao", string.Empty, StringComparison.Ordinal)
            .Replace("ivaohub", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("@ivao/atmosphere", string.Empty, StringComparison.Ordinal)
            .Contains("ivao", StringComparison.OrdinalIgnoreCase);

    /// <summary>A string that is an airport or a FIR as the network writes them: four capital letters, and nothing else.</summary>
    private static bool WritesAnAirport(string line) => AnAirport().IsMatch(line);

    /// <summary>A string that is one of the kinds a fresh calendar is born with: the module chooses no kind, the division does.</summary>
    private static bool WritesAKind(string line, IReadOnlyList<string> kinds) =>
        kinds.Any(kind => line.Contains($"\"{kind}\"", StringComparison.Ordinal) || line.Contains($"'{kind}'", StringComparison.Ordinal));

    /// <summary>
    /// A rating the module decides by itself: a number compared with, assigned to or passed as a rating, or one of the
    /// network's names for a rating or a kind of position written in a string.
    /// </summary>
    private static bool WritesARating(string line, IReadOnlyList<string> words) =>
        NumberAfterARating().IsMatch(line)
        || NumberBeforeARating().IsMatch(line)
        || NumberToTheVocabulary().IsMatch(line)
        || NumberAfterALadder().IsMatch(line)
        || words.Any(word => line.Contains($"\"{word}\"", StringComparison.Ordinal)
            || line.Contains($"'{word}'", StringComparison.Ordinal)
            || line.Contains($"_{word}\"", StringComparison.Ordinal)
            || line.Contains($"_{word}'", StringComparison.Ordinal));

    /// <summary>What the network calls its ratings and its kinds of position, read from the core's own vocabulary.</summary>
    private static List<string> NetworkWords()
    {
        var ratings = Enum.GetValues<RatingKind>().SelectMany(IvaoRatings.Vocabulary.Ladder).ToList();
        return
        [
            .. ratings.Select(rating => rating.ShortName),
            .. ratings.Select(rating => rating.PositionType).OfType<string>(),
        ];
    }

    /// <summary>The kinds of the calendar a fresh installation is born with, read from the seed every release ships.</summary>
    private static List<string> SeededKinds()
    {
        using var seed = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "seed", "calendar-kinds", "kinds.json")));
        return [.. seed.RootElement.GetProperty("kinds").EnumerateArray().Select(kind => kind.GetProperty("key").GetString()!)];
    }

    /// <summary>A word that is a rating in camel case — <c>rating</c>, <c>RatingAtc</c>, <c>MinimumAtcRating</c> — and not <c>operating</c>.</summary>
    private const string ARating = @"\w*(?:(?<![A-Za-z])r|R)ating\w*";

    [GeneratedRegex(ARating + @"\s*(?:[<>]=?|[=!]==?|=(?!>)|:|\bis\b(?:\s+not)?)\s*-?\d")]
    private static partial Regex NumberAfterARating();

    [GeneratedRegex(@"\b\d+\s*(?:[<>]=?|[=!]==?)\s*[\w.]*?" + ARating)]
    private static partial Regex NumberBeforeARating();

    [GeneratedRegex(@"\b(?:Find|NextTraining|IsAtLeast)\s*\([^)]*\b\d+\b")]
    private static partial Regex NumberToTheVocabulary();

    [GeneratedRegex(@"\(\s*RatingKind\.\w+\s*,\s*-?\d")]
    private static partial Regex NumberAfterALadder();

    [GeneratedRegex(@"[""'][A-Z]{4}[""']")]
    private static partial Regex AnAirport();

    [GeneratedRegex(@"ivao\.", RegexOptions.IgnoreCase)]
    private static partial Regex NetworkAddress();

    [GeneratedRegex(@"\b(?:HttpClient|IHttpClientFactory|AddHttpClient|HttpRequestMessage|WebRequest)\b")]
    private static partial Regex AClientOfItsOwn();

    // ---- the files ---------------------------------------------------------------------------------------------------------

    /// <summary>The C# of the module, without what EF Core writes (its migrations) and what the compiler writes.</summary>
    private static IEnumerable<string> ServerSources() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src", "IvaoHub.Modules.Events"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsUnder(file, "Migrations") && !IsUnder(file, "obj") && !IsUnder(file, "bin"));

    /// <summary>The TypeScript of the module's front end, without its tests: a test may write airports and kinds of its own.</summary>
    private static IEnumerable<string> BrowserSources() =>
        Directory.EnumerateFiles(BrowserRoot(), "*.*", SearchOption.AllDirectories)
            .Where(file => (file.EndsWith(".ts", StringComparison.Ordinal) || file.EndsWith(".tsx", StringComparison.Ordinal))
                && !file.Contains(".test.", StringComparison.Ordinal));

    private static IEnumerable<string> LanguageFiles() =>
        Directory.EnumerateFiles(Path.Combine(BrowserRoot(), "locales"), "*.json", SearchOption.AllDirectories);

    private static string BrowserRoot() => Path.Combine(RepositoryRoot(), "web", "src", "modules", EventsModule.ModuleKey);

    private static bool IsUnder(string file, string folder) =>
        file.Contains($"{Path.DirectorySeparatorChar}{folder}{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>Every line of a file that fails, as <c>file:line: text</c>, so that a failure says where.</summary>
    private static IEnumerable<string> Lines(string file, Func<string, bool> fails) =>
        File.ReadLines(file)
            .Select((line, index) => (line, number: index + 1))
            .Where(entry => fails(entry.line))
            .Select(entry => $"{Path.GetFileName(file)}:{entry.number}: {entry.line.Trim()}");

    /// <summary>A line without its comment, for the patterns a sentence could set off: a section of the design is a number too.</summary>
    private static string WithoutComment(string line)
    {
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        var trimmed = line.TrimStart();
        return trimmed.StartsWith('*') || trimmed.StartsWith("/*", StringComparison.Ordinal)
            ? string.Empty
            : comment < 0 ? line : line[..comment];
    }

    /// <summary>The repository, found from the test binaries: the solution file is the marker.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "IvaoHub.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
