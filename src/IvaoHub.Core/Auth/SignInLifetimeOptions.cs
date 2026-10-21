using System.Globalization;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Auth;

/// <summary>
/// How long a sign-in lasts at most, bound from <c>SignIn:</c>. The cookie slides by twelve hours while it is used; past
/// <see cref="MaximumDays"/> from the IVAO sign-in it is refused however it was used, and the next sign-in computes the
/// permissions again. Left out, thirty days, the window a personal token's member must have signed in within
/// (<see cref="PersonalTokens.SignedInWithinDays"/>).
/// </summary>
public sealed class SignInLifetimeOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "SignIn";

    public const int DefaultMaximumDays = PersonalTokens.SignedInWithinDays;

    /// <summary>A year: a longer maximum is no maximum.</summary>
    public const int LongestMaximumDays = 365;

    /// <summary>How many days a sign-in lasts at most; <see cref="DefaultMaximumDays"/> when left out.</summary>
    public int? MaximumDays { get; init; }

    /// <summary>The maximum as a span of time, the default filled in.</summary>
    public TimeSpan MaximumLifetime => TimeSpan.FromDays(MaximumDays ?? DefaultMaximumDays);
}

/// <summary>A maximum of no days, or of more than a year, stops the start with the key to fix.</summary>
public sealed class SignInLifetimeOptionsValidator : IValidateOptions<SignInLifetimeOptions>
{
    public ValidateOptionsResult Validate(string? name, SignInLifetimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.MaximumDays is < 1 or > SignInLifetimeOptions.LongestMaximumDays
            ? ValidateOptionsResult.Fail(string.Create(
                CultureInfo.InvariantCulture,
                $"'{SignInLifetimeOptions.SectionName}:MaximumDays' is {options.MaximumDays}: it is how many days a sign-in lasts at most, from 1 to {SignInLifetimeOptions.LongestMaximumDays}. Leave it out for {SignInLifetimeOptions.DefaultMaximumDays}."))
            : ValidateOptionsResult.Success;
    }
}
