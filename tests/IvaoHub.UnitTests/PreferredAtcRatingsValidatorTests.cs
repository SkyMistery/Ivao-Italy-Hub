using System.Text.Json;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// <c>division.json → preferredAtcRatings</c>, checked at start-up against the kinds and the ratings IVAO has (M4, E10c; the
/// maintainer's answer on #204): a kind or a rating written wrong would put nobody first on the positions it meant, silently,
/// so the start stops and says which key and why.
/// </summary>
public sealed class PreferredAtcRatingsValidatorTests
{
    [Theory]
    [InlineData("division.json")] // Italy's rule
    [InlineData("division.example.json")] // what a fork copies
    [InlineData("division.xx.json")] // the fork test's division, which names none
    public void TheDivisionFilesPass(string file)
    {
        Assert.True(Validate(PreferredIn(file)).Succeeded);
    }

    [Fact]
    public void NoneAtAllPasses()
    {
        Assert.True(Validate([]).Succeeded);
    }

    [Fact]
    public void AKindAndARatingAreReadInAnyCase()
    {
        Assert.True(Validate(new() { ["twr"] = "adc", [" Del "] = "as3" }).Succeeded);
    }

    [Fact]
    public void AKindIvaoDoesNotListStopsTheStart()
    {
        var result = Validate(new() { ["TOWER"] = "ADC" });

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures!);
        Assert.Contains("'preferredAtcRatings' has an entry for 'TOWER'", failure, StringComparison.Ordinal);
        Assert.Contains("DEL, GND, TWR, APP, DEP, ATIS, CTR, FSS", failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("XYZ")]
    [InlineData("PP")] // a pilot's rating
    [InlineData("")]
    public void ARatingThatIsNotAnAtcRatingStopsTheStart(string rating)
    {
        var result = Validate(new() { ["TWR"] = rating });

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures!);
        Assert.Contains($"'preferredAtcRatings.TWR' ({rating}) is not an ATC rating", failure, StringComparison.Ordinal);
        Assert.Contains("AS1, AS2, AS3, ADC, APC, ACC, SEC, SAI, CAI", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryMistakeIsSaidAtOnce()
    {
        var result = Validate(new() { ["TOWER"] = "XYZ", ["GND"] = "ADC", ["APP"] = "PP" });

        Assert.Equal(3, result.Failures!.Count());
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(Dictionary<string, string> preferred) =>
        new PreferredAtcRatingsValidator().Validate(null, new DivisionOptions { PreferredAtcRatings = preferred });

    /// <summary>The key as a division file writes it; none for a file without it.</summary>
    private static Dictionary<string, string> PreferredIn(string file)
    {
        var path = Path.Combine(HubPaths.Resolve(AppContext.BaseDirectory).Root, "config", file);
        using var division = JsonDocument.Parse(File.ReadAllText(path));

        return division.RootElement.TryGetProperty("preferredAtcRatings", out var preferred)
            ? preferred.EnumerateObject().ToDictionary(kind => kind.Name, kind => kind.Value.GetString()!)
            : [];
    }
}
