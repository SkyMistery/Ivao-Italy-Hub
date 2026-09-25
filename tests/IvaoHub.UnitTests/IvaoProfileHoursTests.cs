using System.Text.Json;
using IvaoHub.Core.Auth;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The connection hours of the IVAO profile (M3, A1), read against the answer a real sign in got on 25 September 2026
/// (<c>users-me-790001.json</c>, recorded with <c>tools/record-ivao-fixtures.mjs --me</c>): an array of
/// <c>{ type, hours }</c> rows, one per kind of connection, in seconds. The person is out of the file and the values are
/// invented; the shape is not, and the shape is exactly what was guessed wrong before anybody read the field.
/// </summary>
public sealed class IvaoProfileHoursTests
{
    private static IvaoUserProfile Read(string json) =>
        IvaoUserProfileReader.Read(JsonDocument.Parse(json).RootElement)!;

    [Fact]
    public void TheRecordedProfileGivesItsHoursInHours()
    {
        var profile = IvaoUserProfileReader.Read(IvaoFixtures.Read("users-me-790001.json"))!;

        // 432 000 and 540 000 seconds in the file.
        Assert.Equal(120m, profile.HoursAtc);
        Assert.Equal(150m, profile.HoursPilot);
    }

    [Fact]
    public void TheRecordedProfileIsReadAsBeforeInEverythingElse()
    {
        var profile = IvaoUserProfileReader.Read(IvaoFixtures.Read("users-me-790001.json"))!;

        Assert.Equal(790001, profile.Vid);
        Assert.Equal(6, profile.RatingAtc);
        Assert.Equal(5, profile.RatingPilot);
        Assert.Equal("member-790001@example.invalid", profile.Email);
        Assert.Empty(profile.StaffPositions);
    }

    [Fact]
    public void AProfileWithoutHoursHasNone()
    {
        var profile = Read("""{ "id": 790001 }""");

        Assert.Null(profile.HoursAtc);
        Assert.Null(profile.HoursPilot);
    }

    [Fact]
    public void ZeroHoursAreZeroAndNotNothing()
    {
        // A member who never connected as a pilot has zero hours, which a threshold compares; null is "IVAO said nothing".
        var profile = Read("""
            { "id": 790001, "hours": [ { "type": "pilot", "hours": 0 }, { "type": "atc", "hours": 0 }, { "type": "staff", "hours": 0 } ] }
            """);

        Assert.Equal(0m, profile.HoursAtc);
        Assert.Equal(0m, profile.HoursPilot);
    }

    [Fact]
    public void ARowThatIsMissingIsNotZero()
    {
        var profile = Read("""{ "id": 790001, "hours": [ { "type": "pilot", "hours": 3600 } ] }""");

        Assert.Null(profile.HoursAtc);
        Assert.Equal(1m, profile.HoursPilot);
    }

    [Fact]
    public void SecondsBecomeHoursWithTwoDecimalsAndAreNeverRoundedUp()
    {
        // 7 502 599 s is 2 084.0553 h; 35 999 s is 9.9997 h, which must not become the 10 hours a threshold asks for.
        var profile = Read("""
            { "id": 790001, "hours": [ { "type": "atc", "hours": 7502599 }, { "type": "pilot", "hours": 35999 } ] }
            """);

        Assert.Equal(2084.05m, profile.HoursAtc);
        Assert.Equal(9.99m, profile.HoursPilot);
    }

    [Theory]
    // The shape the test of the reader wrote before the field was read: an object, which IVAO does not send.
    [InlineData("""{ "atc": 100, "pilot": 200 }""")]
    [InlineData("""[ { "type": "atc", "hours": -3600 } ]""")]
    [InlineData("""[ { "type": "atc", "hours": "3600" } ]""")]
    [InlineData("""[ { "type": "atc" } ]""")]
    [InlineData("""[ "atc", 3600 ]""")]
    [InlineData("7200")]
    public void AnythingButARowOfSecondsIsWorthNothingAndNeverAFailedLogin(string hours)
    {
        var profile = Read($$"""{ "id": 790001, "hours": {{hours}} }""");

        Assert.Equal(790001, profile.Vid);
        Assert.Null(profile.HoursAtc);
    }
}
