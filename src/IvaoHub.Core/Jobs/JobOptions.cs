using System.Globalization;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Jobs;

/// <summary>
/// What an installation says about its scheduled jobs (note 2026-10-09-i-job-che-recuperano), bound from <c>Jobs:</c> — a
/// file of <c>secrets/</c> or the <c>Jobs__*</c> environment variables, never the repository (<c>CLAUDE.md</c> §6).
/// </summary>
public sealed class JobOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "Jobs";

    /// <summary>The shortest token the start accepts: a secret nobody guesses, not a word somebody chose.</summary>
    public const int ShortestToken = 32;

    /// <summary>
    /// How long the scheduled task's call waits for the runs when the installation says nothing: the answer has to leave
    /// before the proxies in front give up on it (a hundred seconds at Cloudflare), with the start of a stopped hub in front
    /// of it.
    /// </summary>
    public const int DefaultWaitSeconds = 80;

    /// <summary>The longest wait the start accepts, in seconds: past it every proxy known has given up.</summary>
    public const int LongestWaitSeconds = 600;

    /// <summary>
    /// Whether the hub makes up, a few seconds after it starts and every minute after, the runs it lost while no process
    /// was alive (<see cref="JobCatchUp"/>). Left out, only in <c>Production</c>: a developer's hub, the integration tests
    /// and the end to end bench run their jobs at their hours alone, as before, and say <c>true</c> when they want it.
    /// </summary>
    public bool? CatchUp { get; init; }

    /// <summary>
    /// The token the scheduled task of the host sends to <see cref="JobRunEndpoints.Pattern"/>, as
    /// <c>Authorization: Bearer …</c>. A secret of the installation, not of a person: it is not a personal token, and it
    /// opens that one address and nothing else. Left out, the address does not exist.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// How long the scheduled task's call waits for the runs at most, in seconds; <see cref="DefaultWaitSeconds"/> when left
    /// out. An installation behind a proxy that gives up sooner says less.
    /// </summary>
    public int? WaitSeconds { get; init; }

    /// <summary>The wait of the scheduled task's call.</summary>
    public TimeSpan Wait => TimeSpan.FromSeconds(WaitSeconds ?? DefaultWaitSeconds);
}

/// <summary>
/// A token so short that it could be guessed stops the start, with the key to fix; no token at all is fine. A wait that is
/// not a number of seconds a proxy would wait stops it too.
/// </summary>
public sealed class JobOptionsValidator : IValidateOptions<JobOptions>
{
    public ValidateOptionsResult Validate(string? name, JobOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (!string.IsNullOrEmpty(options.Token) && options.Token.Trim().Length < JobOptions.ShortestToken)
        {
            failures.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"'{JobOptions.SectionName}:Token' is shorter than {JobOptions.ShortestToken} characters: the scheduled task's token must be a secret nobody can guess. Write a longer one, or leave the key out."));
        }

        if (options.WaitSeconds is { } seconds && seconds is < 1 or > JobOptions.LongestWaitSeconds)
        {
            failures.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"'{JobOptions.SectionName}:WaitSeconds' is {seconds}: it is how long the scheduled task's call waits for the runs, between 1 and {JobOptions.LongestWaitSeconds} seconds. Leave it out for {JobOptions.DefaultWaitSeconds}."));
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
