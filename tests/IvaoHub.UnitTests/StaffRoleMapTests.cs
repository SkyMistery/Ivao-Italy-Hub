using IvaoHub.Core.Auth;
using IvaoHub.Core.Division;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The whole table of plan section 4.1, row by row, for a two letter and a three letter division.
/// Getting one of these wrong takes away somebody's access, or hands it to the wrong person, and
/// does it silently: this is the reason the table lives in the code and not in configuration.
/// </summary>
public sealed class StaffRoleMapTests
{
    private static readonly IReadOnlySet<string> ItalianFirs = new HashSet<string> { "LIRR", "LIMM", "LIBB" };
    private static readonly IReadOnlySet<string> NoFirs = new HashSet<string>();

    [Theory]
    // Division headquarters
    [InlineData("IT-DIR", Department.HQ, StaffLevel.Coordinator, StaffRole.Director)]
    [InlineData("IT-ADIR", Department.HQ, StaffLevel.Assistant, StaffRole.Director)]
    // Special operations
    [InlineData("IT-SOC", Department.SO, StaffLevel.Coordinator, StaffRole.SpecialOps)]
    [InlineData("IT-SOAC", Department.SO, StaffLevel.Assistant, StaffRole.SpecialOps)]
    [InlineData("IT-SOA1", Department.SO, StaffLevel.Advisor, StaffRole.SpecialOps)]
    [InlineData("IT-SOA9", Department.SO, StaffLevel.Advisor, StaffRole.SpecialOps)]
    // Flight operations
    [InlineData("IT-FOC", Department.FO, StaffLevel.Coordinator, StaffRole.FlightOps)]
    [InlineData("IT-FOAC", Department.FO, StaffLevel.Assistant, StaffRole.FlightOps)]
    [InlineData("IT-FOA3", Department.FO, StaffLevel.Advisor, StaffRole.FlightOps)]
    // ATC operations
    [InlineData("IT-AOC", Department.AO, StaffLevel.Coordinator, StaffRole.AtcOps)]
    [InlineData("IT-AOAC", Department.AO, StaffLevel.Assistant, StaffRole.AtcOps)]
    [InlineData("IT-AOA1", Department.AO, StaffLevel.Advisor, StaffRole.AtcOps)]
    // Training
    [InlineData("IT-TC", Department.TR, StaffLevel.Coordinator, StaffRole.Training)]
    [InlineData("IT-TAC", Department.TR, StaffLevel.Assistant, StaffRole.Training)]
    [InlineData("IT-TA1", Department.TR, StaffLevel.Advisor, StaffRole.Training)]
    [InlineData("IT-TA9", Department.TR, StaffLevel.Advisor, StaffRole.Training)]
    // Trainers
    [InlineData("IT-T01", Department.TR, StaffLevel.Member, StaffRole.Trainer)]
    [InlineData("IT-T09", Department.TR, StaffLevel.Member, StaffRole.Trainer)]
    [InlineData("IT-T10", Department.TR, StaffLevel.Member, StaffRole.Trainer)]
    [InlineData("IT-T99", Department.TR, StaffLevel.Member, StaffRole.Trainer)]
    // Membership
    [InlineData("IT-MC", Department.MB, StaffLevel.Coordinator, StaffRole.Membership)]
    [InlineData("IT-MAC", Department.MB, StaffLevel.Assistant, StaffRole.Membership)]
    [InlineData("IT-MA2", Department.MB, StaffLevel.Advisor, StaffRole.Membership)]
    // Events
    [InlineData("IT-EC", Department.EV, StaffLevel.Coordinator, StaffRole.Events)]
    [InlineData("IT-EAC", Department.EV, StaffLevel.Assistant, StaffRole.Events)]
    [InlineData("IT-EA1", Department.EV, StaffLevel.Advisor, StaffRole.Events)]
    // Public relations
    [InlineData("IT-PRC", Department.PR, StaffLevel.Coordinator, StaffRole.PublicRelations)]
    [InlineData("IT-PRAC", Department.PR, StaffLevel.Assistant, StaffRole.PublicRelations)]
    [InlineData("IT-PRA1", Department.PR, StaffLevel.Advisor, StaffRole.PublicRelations)]
    // Web development
    [InlineData("IT-WM", Department.WM, StaffLevel.Coordinator, StaffRole.Web)]
    [InlineData("IT-AWM", Department.WM, StaffLevel.Assistant, StaffRole.Web)]
    [InlineData("IT-WMA1", Department.WM, StaffLevel.Advisor, StaffRole.Web)]
    public void ReadsEveryDivisionalPosition(string raw, Department department, StaffLevel level, StaffRole role)
    {
        var parsed = StaffRoleMap.Parse(raw, "IT", ItalianFirs);

        Assert.NotNull(parsed);
        Assert.Equal(department, parsed.Department);
        Assert.Equal(level, parsed.Level);
        Assert.Equal(role, parsed.Role);
        Assert.Null(parsed.Fir);
        Assert.Equal(raw, parsed.Raw);
    }

    [Theory]
    [InlineData("LIRR-CH", "LIRR", StaffLevel.Coordinator, StaffRole.FirChief)]
    [InlineData("LIRR-ACH", "LIRR", StaffLevel.Assistant, StaffRole.FirAssistantChief)]
    [InlineData("LIRR-CHA1", "LIRR", StaffLevel.Advisor, StaffRole.FirAdvisor)]
    [InlineData("LIMM-CH", "LIMM", StaffLevel.Coordinator, StaffRole.FirChief)]
    [InlineData("LIMM-CHA9", "LIMM", StaffLevel.Advisor, StaffRole.FirAdvisor)]
    public void ReadsFirPositionsOfTheDivision(string raw, string fir, StaffLevel level, StaffRole role)
    {
        var parsed = StaffRoleMap.Parse(raw, "IT", ItalianFirs);

        Assert.NotNull(parsed);
        Assert.Null(parsed.Department);
        Assert.Equal(fir, parsed.Fir);
        Assert.Equal(level, parsed.Level);
        Assert.Equal(role, parsed.Role);
    }

    [Fact]
    public void DoesNotRecogniseAFirPositionBeforeTheReferenceDataIsSynchronised()
    {
        // F3 fills ref_ivao_centers; until then the position is kept raw and is worth nothing.
        Assert.Null(StaffRoleMap.Parse("LIRR-CH", "IT", NoFirs));
    }

    [Theory]
    [InlineData("XX-EC", "XX", Department.EV, StaffRole.Events)]
    [InlineData("XX-DIR", "XX", Department.HQ, StaffRole.Director)]
    [InlineData("XXX-EC", "XXX", Department.EV, StaffRole.Events)]
    [InlineData("XXX-WM", "XXX", Department.WM, StaffRole.Web)]
    public void WorksForAnyDivisionCode(string raw, string divisionCode, Department department, StaffRole role)
    {
        var parsed = StaffRoleMap.Parse(raw, divisionCode, NoFirs);

        Assert.NotNull(parsed);
        Assert.Equal(department, parsed.Department);
        Assert.Equal(role, parsed.Role);
    }

    [Theory]
    [InlineData("FR-DIR")]
    [InlineData("IT-T100")]
    [InlineData("IT-T0")]
    [InlineData("IT-T00")]
    [InlineData("IT-TA0")]
    [InlineData("IT-SOA0")]
    [InlineData("IT-XYZ")]
    [InlineData("IT-")]
    [InlineData("IT")]
    [InlineData("LIRR-XX")]
    [InlineData("")]
    [InlineData(null)]
    public void IgnoresWhatItDoesNotRecognise(string? raw)
    {
        Assert.Null(StaffRoleMap.Parse(raw, "IT", ItalianFirs));
    }

    [Fact]
    public void ReadsHeadquartersPositionsAsReadOnlyAndWithoutADepartment()
    {
        var parsed = StaffRoleMap.Parse("HQ-EC", "IT", ItalianFirs);

        Assert.NotNull(parsed);
        Assert.Equal(StaffRole.HqStaff, parsed.Role);
        Assert.Null(parsed.Department);
        Assert.Null(parsed.Fir);
    }

    [Fact]
    public void IsCaseInsensitiveAndKeepsTheNormalisedForm()
    {
        var parsed = StaffRoleMap.Parse("  it-ec  ", "IT", ItalianFirs);

        Assert.NotNull(parsed);
        Assert.Equal("IT-EC", parsed.Raw);
        Assert.Equal(Department.EV, parsed.Department);
    }

    [Fact]
    public void TriesTheMostSpecificTrainingPatternFirst()
    {
        // The order of the rules is the whole point: TAC must not be read as a TA advisor, and
        // T01 must not be read as anything else (plan section 4.1).
        Assert.Equal(StaffRole.Trainer, StaffRoleMap.Parse("IT-T01", "IT", NoFirs)!.Role);
        Assert.Equal(StaffLevel.Advisor, StaffRoleMap.Parse("IT-TA1", "IT", NoFirs)!.Level);
        Assert.Equal(StaffLevel.Assistant, StaffRoleMap.Parse("IT-TAC", "IT", NoFirs)!.Level);
        Assert.Equal(StaffLevel.Coordinator, StaffRoleMap.Parse("IT-TC", "IT", NoFirs)!.Level);
    }
}
