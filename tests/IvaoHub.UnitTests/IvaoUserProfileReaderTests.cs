using System.Text.Json;
using IvaoHub.Core.Auth;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The shape of the IVAO user info payload, as measured against the real one on 3 September 2026
/// with the scopes openid, profile, email and discord. The values below are invented; the field
/// names and their shapes are not, and that is the whole point of this test: getting a field name
/// wrong does not throw, it silently removes an identity or a role.
/// </summary>
public sealed class IvaoUserProfileReaderTests
{
    private const string RealShape = """
        {
          "id": 704798,
          "firstName": "Mario",
          "lastName": "Rossi",
          "centerId": "LIRR",
          "countryId": "IT",
          "createdAt": "2010-01-01T00:00:00.000Z",
          "divisionId": "IT",
          "isStaff": true,
          "isSupervisor": false,
          "languageId": "it",
          "email": "someone@example.invalid",
          "rating": { "atcRating": { "id": 7 }, "pilotRating": { "id": 5 } },
          "hours": { "atc": 100, "pilot": 200 },
          "userStaffPositions": [
            { "id": "IT-AOA1", "connectAs": "IT-AOA1" },
            { "id": "IT-T03", "connectAs": "IT-T03" }
          ],
          "sub": "704798",
          "given_name": "Mario",
          "family_name": "Rossi",
          "nickname": "Mario",
          "publicNickname": "Mario (704798)",
          "profile": "https://www.ivao.aero/Member.aspx?id=704798"
        }
        """;

    private static IvaoUserProfile Read(string json) =>
        IvaoUserProfileReader.Read(JsonDocument.Parse(json).RootElement)!;

    [Fact]
    public void ReadsTheFieldsTheHubKeeps()
    {
        var profile = Read(RealShape);

        Assert.Equal(704798, profile.Vid);
        Assert.Equal("Mario", profile.FirstName);
        Assert.Equal("Rossi", profile.LastName);
        Assert.Equal("IT", profile.DivisionCode);
        Assert.Equal("IT", profile.CountryId);
        Assert.Equal("it", profile.LanguageId);
        Assert.Equal(7, profile.RatingAtc);
        Assert.Equal(5, profile.RatingPilot);
        Assert.True(profile.IvaoIsStaff);
        Assert.False(profile.IvaoIsSupervisor);
        Assert.Equal(["IT-AOA1", "IT-T03"], profile.StaffPositions);
    }

    [Fact]
    public void DoesNotReadADiscordIdBecauseIvaoDoesNotSendOne()
    {
        // Measured, not assumed: the payload carries no Discord field at all, even with the
        // discord scope granted. The division links Discord with a tool of its own.
        Assert.Null(Read(RealShape).DiscordId);
    }

    [Fact]
    public void KeepsNoEmailAddress()
    {
        // The hub stores the minimum IVAO data it needs (plan section 6.4). The payload carries an
        // email address; nothing here reads it, and there is nowhere to put it.
        Assert.DoesNotContain(
            typeof(IvaoUserProfile).GetProperties(),
            property => property.Name.Contains("mail", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TreatsThePlaceholderNicknameAsNoNickname()
    {
        var profile = Read("""{ "id": 12345, "publicNickname": "User 12345" }""");

        Assert.Null(profile.PublicNickname);
    }

    [Fact]
    public void FallsBackToTheStandardOpenIdNames()
    {
        var profile = Read("""{ "sub": "999", "given_name": "Ada", "family_name": "Lovelace" }""");

        Assert.Equal(999, profile.Vid);
        Assert.Equal("Ada", profile.FirstName);
        Assert.Equal("Lovelace", profile.LastName);
    }

    [Fact]
    public void SurvivesAFieldThatChangesShape()
    {
        // A field that changes shape must cost a null, never a failed login.
        var profile = Read("""
            { "id": 1, "rating": { "atcRating": "not a number" }, "isStaff": "maybe",
              "userStaffPositions": "not an array" }
            """);

        Assert.Null(profile.RatingAtc);
        Assert.Null(profile.IvaoIsStaff);
        Assert.Empty(profile.StaffPositions);
    }

    [Fact]
    public void RefusesAPayloadWithoutAVid()
    {
        Assert.Null(IvaoUserProfileReader.Read(JsonDocument.Parse("""{ "firstName": "Nobody" }""").RootElement));
    }
}
