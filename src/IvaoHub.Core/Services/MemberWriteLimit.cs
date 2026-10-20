using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Services;

/// <summary>
/// The core's limit on the writing verbs of members, as <c>/auth</c> already has one (note
/// 2026-10-19-il-limite-dei-verbi-dei-membri): a module applies it by name to its member groups,
/// <c>mine.RequireRateLimiting(HubRateLimits.MemberWrites)</c>, and writes nothing else. It counts each signed in member
/// apart, by VID, and a caller nobody signed in by address; a read passes untouched, so a whole group takes it.
/// </summary>
public static class HubRateLimits
{
    /// <summary>The policy's name, the one a module writes.</summary>
    public const string MemberWrites = "member-writes";

    /// <summary>The title of the 429, resolved in the asker's language like every refusal of the core.</summary>
    public const string TooManyRequestsTitleKey = "errors.tooManyRequests.title";

    /// <summary>
    /// Registers <see cref="MemberWrites"/>. The numbers are read from <see cref="MemberWriteLimitOptions"/> when a partition
    /// is first used, so a test host and a deployment may set them after this.
    /// </summary>
    public static RateLimiterOptions AddMemberWritesPolicy(this RateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(MemberWrites, context =>
        {
            if (HttpMethods.IsGet(context.Request.Method)
                || HttpMethods.IsHead(context.Request.Method)
                || HttpMethods.IsOptions(context.Request.Method))
            {
                return RateLimitPartition.GetNoLimiter("read");
            }

            var vid = context.User.FindFirstValue(HubClaims.Vid);
            var partition = string.IsNullOrEmpty(vid)
                ? "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown")
                : "vid:" + vid;

            return RateLimitPartition.GetFixedWindowLimiter(partition, _ =>
            {
                var limit = context.RequestServices.GetRequiredService<IOptions<MemberWriteLimitOptions>>().Value;
                return new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit.PermitLimit ?? MemberWriteLimitOptions.DefaultPermitLimit,
                    Window = TimeSpan.FromSeconds(limit.WindowSeconds ?? MemberWriteLimitOptions.DefaultWindowSeconds),
                    QueueLimit = 0,
                };
            });
        });

        return options;
    }

    /// <summary>
    /// The answer of every limit of the hub, the login's included: 429 with a title in the asker's language through the
    /// one <see cref="IProblemDetailsService"/>, and <c>Retry-After</c> when the window says when it opens again.
    /// </summary>
    public static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var http = context.HttpContext;
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var catalog = http.RequestServices.GetRequiredService<LocaleCatalog>();
        var currentUser = http.RequestServices.GetRequiredService<ICurrentUser>();

        await http.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = catalog.Resolve(currentUser.Locale, TooManyRequestsTitleKey),
            },
        });
    }
}

/// <summary>
/// What an installation says about <see cref="HubRateLimits.MemberWrites"/>, bound from <c>RateLimits:MemberWrites:</c>; left
/// out, thirty writes a minute for each member, more than a person filling in the forms of the hub ever sends.
/// </summary>
public sealed class MemberWriteLimitOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "RateLimits:MemberWrites";

    public const int DefaultPermitLimit = 30;

    public const int DefaultWindowSeconds = 60;

    /// <summary>How many writes a member sends in one window; <see cref="DefaultPermitLimit"/> when left out.</summary>
    public int? PermitLimit { get; init; }

    /// <summary>How long a window lasts, in seconds; <see cref="DefaultWindowSeconds"/> when left out.</summary>
    public int? WindowSeconds { get; init; }
}

/// <summary>A limit of no writes, or a window of no time, stops the start with the key to fix.</summary>
public sealed class MemberWriteLimitOptionsValidator : IValidateOptions<MemberWriteLimitOptions>
{
    public ValidateOptionsResult Validate(string? name, MemberWriteLimitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.PermitLimit is < 1)
        {
            failures.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"'{MemberWriteLimitOptions.SectionName}:PermitLimit' is {options.PermitLimit}: it is how many writes a member sends in a window, at least 1. Leave it out for {MemberWriteLimitOptions.DefaultPermitLimit}."));
        }

        if (options.WindowSeconds is < 1)
        {
            failures.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"'{MemberWriteLimitOptions.SectionName}:WindowSeconds' is {options.WindowSeconds}: it is how long a window lasts, at least 1 second. Leave it out for {MemberWriteLimitOptions.DefaultWindowSeconds}."));
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

public static class MemberWriteLimitServiceCollectionExtensions
{
    /// <summary>The numbers of <see cref="HubRateLimits.MemberWrites"/>, checked at the start.</summary>
    public static IServiceCollection AddMemberWriteLimit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<MemberWriteLimitOptions>().BindConfiguration(MemberWriteLimitOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<MemberWriteLimitOptions>, MemberWriteLimitOptionsValidator>();

        return services;
    }
}
