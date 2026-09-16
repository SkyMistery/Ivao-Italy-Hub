using IvaoHub.Core.Auth;
using IvaoHub.Core.Division;
using IvaoHub.Modules.FlightOps.Settings;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The pieces of T5 that need no database: what makes two grants of the division file the same grant, and
/// the rules of the settings of the tours as the design proposes them (§1.11).
/// </summary>
public sealed class FlightOpsSkeletonTests
{
    [Fact]
    public void ReorderingTheLevelsOfASeedDoesNotMakeItANewOne()
    {
        var one = new PositionGrantSeed
        {
            Department = Department.FOD,
            Levels = [StaffLevel.Coordinator, StaffLevel.Assistant],
            Permission = "Tours.View",
            Scope = Department.FOD,
        };
        var reordered = new PositionGrantSeed
        {
            Department = Department.FOD,
            Levels = [StaffLevel.Assistant, StaffLevel.Coordinator],
            Permission = "Tours.View",
            Scope = Department.FOD,
        };
        var denied = new PositionGrantSeed
        {
            Department = Department.FOD,
            Levels = [StaffLevel.Coordinator, StaffLevel.Assistant],
            Permission = "Tours.View",
            Scope = Department.FOD,
            Deny = true,
        };

        Assert.Equal(PositionGrantSeeder.Fingerprint(one), PositionGrantSeeder.Fingerprint(reordered));
        Assert.NotEqual(PositionGrantSeeder.Fingerprint(one), PositionGrantSeeder.Fingerprint(denied));
    }

    [Fact]
    public void TheDefaultsOfTheSettingsAreValidAndCarryNoCountry()
    {
        var defaults = new FlightOpsSettings();

        Assert.True(new FlightOpsSettingsValidator().Validate(defaults).IsValid);
        Assert.Empty(defaults.NorthSouthLevelCountries);
    }

    [Fact]
    public void ALimitSwitchedOffIsValidAndAShorterDisciplinaryRecordIsNot()
    {
        var validator = new FlightOpsSettingsValidator();

        Assert.True(validator.Validate(new FlightOpsSettings { DailyLegLimit = null }).IsValid);

        var shorter = validator.Validate(new FlightOpsSettings { RetentionMonths = 13, RetentionMonthsLong = 6 });
        Assert.Contains(shorter.Errors, error => error.ErrorMessage == "flightops:errors.retentionLongShorter");

        var country = validator.Validate(new FlightOpsSettings { NorthSouthLevelCountries = ["xx1"] });
        Assert.Contains(country.Errors, error => error.ErrorMessage == "flightops:errors.countryCode");
    }
}
