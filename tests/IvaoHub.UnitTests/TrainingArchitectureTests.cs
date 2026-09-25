using System.Text.Json;
using System.Text.RegularExpressions;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// What design M3 §10 asks of the training and of no other module, so it lives here and not among the architecture tests of
/// the whole hub (note 2026-09-25-rating-e-postazioni-dal-nucleo §4): the module does not name the network — its ratings and
/// its positions are the core's to answer (§1.7) —, it writes no rating of its own, and it calls nobody outside the hub. And
/// the division file gives the training department what the design says (§3.2).
/// <para>They read the sources, as the architecture tests do, so they are patterns and not proofs: they catch the way these
/// rules get broken by habit — a number next to a rating, the network's word in a string — and the review still reads the
/// rest. The theories at the bottom show what each pattern catches and what it lets through.</para>
/// </summary>
public sealed partial class TrainingArchitectureTests
{
    [Fact]
    public void TheModuleDoesNotNameTheNetwork()
    {
        // The code, server and browser: the product's own name is not the network's, and the core's perimeter of the network
        // is where the module takes its ratings and positions from — nor is the design system the hub is built with.
        var inTheCode = ServerSources().Concat(BrowserSources())
            .SelectMany(file => Lines(file, NamesTheNetwork));

        // The words: a language file may say what the network is to a member, but never where it lives. The site of the
        // exam is a setting for that reason (§12 n.12).
        var inTheWords = LanguageFiles()
            .SelectMany(file => Lines(file, line => NetworkAddress().IsMatch(line)));

        Assert.Empty(inTheCode.Concat(inTheWords));
    }

    [Fact]
    public void TheModuleWritesNoRatingOfItsOwn()
    {
        var words = NetworkWords();

        var offenders = ServerSources().Concat(BrowserSources())
            .SelectMany(file => Lines(file, line => WritesARating(WithoutComment(line), words)));

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
    public void TheDivisionFilesGiveTheTrainingDepartmentWhatTheDesignSays()
    {
        StaffLevel[] everybody = [StaffLevel.Coordinator, StaffLevel.Assistant, StaffLevel.Advisor, StaffLevel.Member];
        StaffLevel[] advisors = [StaffLevel.Coordinator, StaffLevel.Assistant, StaffLevel.Advisor];
        StaffLevel[] heads = [StaffLevel.Coordinator, StaffLevel.Assistant];

        // Design M3 §3.2: coordinator and assistant everything, the advisors (TA1–9) view, approve and put exams in the
        // calendar, the trainers (T01–T99) view and put exams in the calendar. A trainer conducts only the trainings assigned
        // to them, through a grant on the one row (§3.3, A7), so conducting is not given here.
        var design = new Dictionary<string, StaffLevel[]>(StringComparer.Ordinal)
        {
            [TrainingPermissions.View] = everybody,
            [TrainingPermissions.Approve] = advisors,
            [TrainingPermissions.Assign] = heads,
            [TrainingPermissions.Conduct] = heads,
            [TrainingPermissions.Edit] = heads,
            [TrainingPermissions.ManageSheets] = heads,
            [TrainingPermissions.ManageExams] = everybody,
            [TrainingPermissions.Ban] = heads,
            [TrainingPermissions.ManageSettings] = heads,
        };

        Assert.Equal(TrainingPermissions.All.Select(permission => permission.Name).Order(StringComparer.Ordinal), design.Keys.Order(StringComparer.Ordinal));

        foreach (var file in new[] { "division.json", "division.example.json" })
        {
            using var division = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "config", file)));
            var root = division.RootElement;

            Assert.Equal(
                nameof(Department.TD),
                root.GetProperty("modules").GetProperty(TrainingModule.ModuleKey).GetProperty("baseDepartment").GetString());

            var grants = root.GetProperty("positionGrants").EnumerateArray()
                .Where(grant => grant.GetProperty("permission").GetString()!.StartsWith($"{TrainingPermissions.Area}.", StringComparison.Ordinal))
                .ToList();

            // Once each, to the training department, held on it.
            Assert.Equal(design.Count, grants.Count);

            foreach (var grant in grants)
            {
                var permission = grant.GetProperty("permission").GetString()!;
                Assert.Equal(nameof(Department.TD), grant.GetProperty("department").GetString());
                Assert.Equal(nameof(Department.TD), grant.GetProperty("scope").GetString());
                Assert.Equal(
                    design[permission].Order(),
                    grant.GetProperty("levels").EnumerateArray().Select(level => Enum.Parse<StaffLevel>(level.GetString()!)).Order());
            }
        }
    }

    [Theory]
    [InlineData("using IvaoHub.Core.Ivao;", false)]
    [InlineData("namespace IvaoHub.Modules.Training.Settings;", false)]
    [InlineData("\"Server=localhost;Database=ivaohub;User ID=ivaohub\"", false)]
    [InlineData("import { Button } from '@ivao/atmosphere-react';", false)]
    [InlineData("private readonly IIvaoApiClient _client;", true)]
    [InlineData("var ladder = IvaoRatings.Vocabulary.Ladder(kind);", true)]
    [InlineData("/// <summary>The ratings IVAO gives.</summary>", true)]
    [InlineData("const exam = 'https://training.ivao.aero';", true)]
    public void TheNetworkIsNamedWhereThePatternSays(string line, bool named) => Assert.Equal(named, NamesTheNetwork(line));

    [Theory]
    [InlineData("if (trainee.RatingAtc >= 5)", true)]
    [InlineData("return rating is 5 or 6 or 7;", true)]
    [InlineData("if (5 <= user.RatingPilot)", true)]
    [InlineData("var threshold = new HoursThreshold(RatingKind.Atc, 5, 50);", true)]
    [InlineData("var adc = vocabulary.Find(kind, 5);", true)]
    [InlineData("const trainee = { ratingAtc: 4 };", true)]
    [InlineData("var tower = positions.Where(position => position.Callsign.EndsWith(\"_TWR\"));", true)]
    [InlineData("if (rating.ShortName == \"ADC\")", true)]
    [InlineData("const next = rating === 'SPP';", true)]
    [InlineData("Where(rating => rating.HasPracticalTraining)", false)]
    [InlineData("configurationBuilder.Properties<RatingKind>().HaveConversion<string>().HaveMaxLength(8);", false)]
    [InlineData("new TrainingRatingDto(rating.Kind, rating.Number, rating.ShortName, rating.NameKey)", false)]
    [InlineData("rating: z.string().min(1).meta({ choices: choices.ratings }),", false)]
    [InlineData("var operatingHours = 5;", false)]
    [InlineData("var next = vocabulary.NextTraining(kind, trainee.RatingAtc);", false)]
    public void ARatingIsWrittenWhereThePatternSays(string line, bool written) =>
        Assert.Equal(written, WritesARating(line, NetworkWords()));

    // ---- the patterns ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The network's name anywhere, except inside the product's own (<c>IvaoHub</c>, and <c>ivaohub</c> for its database),
    /// the core's perimeter of the network (<c>IvaoHub.Core.Ivao</c>, where the vocabulary and the directory are) and the
    /// package of the design system.
    /// </summary>
    private static bool NamesTheNetwork(string line) =>
        line.Replace("IvaoHub.Core.Ivao", string.Empty, StringComparison.Ordinal)
            .Replace("ivaohub", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("@ivao/atmosphere", string.Empty, StringComparison.Ordinal)
            .Contains("ivao", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>A word that is a rating in camel case — <c>rating</c>, <c>RatingAtc</c>, <c>TraineeRatingAtRequest</c> — and not <c>operating</c>.</summary>
    private const string ARating = @"\w*(?:(?<![A-Za-z])r|R)ating\w*";

    [GeneratedRegex(ARating + @"\s*(?:[<>]=?|[=!]==?|=(?!>)|:|\bis\b(?:\s+not)?)\s*-?\d")]
    private static partial Regex NumberAfterARating();

    [GeneratedRegex(@"\b\d+\s*(?:[<>]=?|[=!]==?)\s*[\w.]*?" + ARating)]
    private static partial Regex NumberBeforeARating();

    [GeneratedRegex(@"\b(?:Find|NextTraining|IsAtLeast)\s*\([^)]*\b\d+\b")]
    private static partial Regex NumberToTheVocabulary();

    [GeneratedRegex(@"\(\s*RatingKind\.\w+\s*,\s*-?\d")]
    private static partial Regex NumberAfterALadder();

    [GeneratedRegex(@"ivao\.", RegexOptions.IgnoreCase)]
    private static partial Regex NetworkAddress();

    [GeneratedRegex(@"\b(?:HttpClient|IHttpClientFactory|AddHttpClient|HttpRequestMessage|WebRequest)\b")]
    private static partial Regex AClientOfItsOwn();

    // ---- the files ---------------------------------------------------------------------------------------------------------

    /// <summary>The C# of the module, without what EF Core writes (its migrations) and what the compiler writes.</summary>
    private static IEnumerable<string> ServerSources() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src", "IvaoHub.Modules.Training"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsUnder(file, "Migrations") && !IsUnder(file, "obj") && !IsUnder(file, "bin"));

    /// <summary>The TypeScript of the module's front end, without its tests: a test may build ratings of its own (§10).</summary>
    private static IEnumerable<string> BrowserSources() =>
        Directory.EnumerateFiles(BrowserRoot(), "*.*", SearchOption.AllDirectories)
            .Where(file => (file.EndsWith(".ts", StringComparison.Ordinal) || file.EndsWith(".tsx", StringComparison.Ordinal))
                && !file.Contains(".test.", StringComparison.Ordinal));

    private static IEnumerable<string> LanguageFiles() =>
        Directory.EnumerateFiles(Path.Combine(BrowserRoot(), "locales"), "*.json", SearchOption.AllDirectories);

    private static string BrowserRoot() => Path.Combine(RepositoryRoot(), "web", "src", "modules", TrainingModule.ModuleKey);

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
