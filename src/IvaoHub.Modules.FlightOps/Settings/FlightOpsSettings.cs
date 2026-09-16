using System.Text.RegularExpressions;
using FluentValidation;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.FlightOps.Settings;

/// <summary>
/// What the flight operations department changes from the interface without a release (design M2 §1.11).
/// The values below are the ones the design proposes and Carmine confirmed on 15 September 2026; they are
/// the defaults of an installation that never saved, and of a setting added after it did.
/// <para>The thresholds of the checks are not here: they are parameters of the rules (§1.7). The window
/// the weather is kept for is not here either — it follows the open tours, and T16 decides how.</para>
/// </summary>
public sealed record FlightOpsSettings
{
    /// <summary>Legs a pilot may report in a day, across tours; null switches the limit off (§3.7).</summary>
    public int? DailyLegLimit { get; init; } = 10;

    /// <summary>Days after the flight within which it may be reported, unless the tour says otherwise.</summary>
    public int DefaultReportWindowDays { get; init; } = 7;

    /// <summary>Days after a decision within which the pilot may dispute it, the same for every tour.</summary>
    public int DisputeWindowDays { get; init; } = 7;

    /// <summary>Hours a rejected leg may be reported again before the next one is blocked (§2.5).</summary>
    public int RejectGraceHours { get; init; } = 12;

    /// <summary>How long a PIREP taken by a validator stays theirs (§4.2).</summary>
    public int LeaseMinutes { get; init; } = 30;

    /// <summary><c>k</c> of the estimated time: the share the route adds to the great circle (§1.5).</summary>
    public decimal DurationFactor { get; init; } = 0.05m;

    /// <summary><c>c</c> of the estimated time: climb, descent, approach and taxi, in minutes (§1.5).</summary>
    public int DurationFixedMinutes { get; init; } = 20;

    /// <summary>
    /// The countries, as two letter codes, where semicircular levels go north and south rather than east and
    /// west (§6.4). Empty until the department says: a code in the code would be a country in the code.
    /// </summary>
    public IReadOnlyList<string> NorthSouthLevelCountries { get; init; } = [];

    /// <summary>Months the reports and their tracks are kept (§10).</summary>
    public int RetentionMonths { get; init; } = 13;

    /// <summary>Months the disciplinary record is kept (§10).</summary>
    public int RetentionMonthsLong { get; init; } = 25;

    /// <summary>How far from the threshold a take-off may start, in metres, one value for the whole system (§6.4).</summary>
    public int ThresholdToleranceMeters { get; init; } = 150;
}

/// <summary>The rules the settings have to satisfy. Messages are i18n keys.</summary>
public sealed partial class FlightOpsSettingsValidator : AbstractValidator<FlightOpsSettings>
{
    public FlightOpsSettingsValidator()
    {
        RuleFor(settings => settings.DailyLegLimit).InclusiveBetween(1, 100).When(settings => settings.DailyLegLimit is not null)
            .WithMessage("errors.number.range");
        RuleFor(settings => settings.DefaultReportWindowDays).InclusiveBetween(1, 60).WithMessage("errors.number.range");
        RuleFor(settings => settings.DisputeWindowDays).InclusiveBetween(1, 60).WithMessage("errors.number.range");
        RuleFor(settings => settings.RejectGraceHours).InclusiveBetween(0, 168).WithMessage("errors.number.range");
        RuleFor(settings => settings.LeaseMinutes).InclusiveBetween(5, 480).WithMessage("errors.number.range");
        RuleFor(settings => settings.DurationFactor).InclusiveBetween(0m, 1m).WithMessage("errors.number.range");
        RuleFor(settings => settings.DurationFixedMinutes).InclusiveBetween(0, 180).WithMessage("errors.number.range");
        RuleFor(settings => settings.RetentionMonths).InclusiveBetween(1, 120).WithMessage("errors.number.range");
        RuleFor(settings => settings.RetentionMonthsLong)
            .InclusiveBetween(1, 240).WithMessage("errors.number.range")
            .GreaterThanOrEqualTo(settings => settings.RetentionMonths).WithMessage("flightops:errors.retentionLongShorter");
        RuleFor(settings => settings.ThresholdToleranceMeters).InclusiveBetween(10, 3000).WithMessage("errors.number.range");
        RuleForEach(settings => settings.NorthSouthLevelCountries)
            .Must(code => code is not null && CountryCode().IsMatch(code))
            .WithMessage("flightops:errors.countryCode");
    }

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryCode();
}

/// <summary>
/// The rules of a save of the settings: those of the values themselves, and the one that needs the tours (design M2
/// §3.7) — the division's daily limit is not switched off while a tour that is still ahead has no limit of its own.
/// Which tours those are, the list of tours answers with the same expression
/// (<c>filter[needsOwnDailyLimit]=true</c>), so the screen can show them.
/// </summary>
public sealed class FlightOpsSettingsSaveValidator : AbstractValidator<FlightOpsSettings>
{
    public FlightOpsSettingsSaveValidator(FlightOpsDbContext database, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);

        Include(new FlightOpsSettingsValidator());

        RuleFor(settings => settings.DailyLegLimit)
            .MustAsync(async (limit, cancellationToken) =>
                limit is not null
                || !await CrudSource.BackOffice<Tour>(database).AnyAsync(TourState.NeedsOwnDailyLimit(clock.UtcNow), cancellationToken))
            .WithMessage("flightops:errors.toursNeedDailyLimit");
    }
}
