using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Auth;

/// <summary>
/// The Discord application of this installation, from <c>secrets/*.json</c> or <c>Discord__*</c> environment variables. All of
/// it optional: an installation without the section has no link with Discord and is otherwise the same hub (note
/// 2026-11-05-il-collegamento-con-discord §5).
/// </summary>
public sealed record DiscordOAuthOptions
{
    /// <summary>Configuration section that holds these values.</summary>
    public const string SectionName = "Discord";

    public string ClientId { get; init; } = string.Empty;

    /// <summary>Never logged, never returned by an endpoint, never written to the diagnostics file.</summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>Must match, character for character, a redirect registered with the Discord application.</summary>
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>Whether this installation offers the link. A section filled in halfway never gets this far: the hub refuses to start.</summary>
    public bool IsConfigured => ClientId.Length > 0 && ClientSecret.Length > 0 && RedirectUri.Length > 0;
}

/// <summary>Nothing, or all of it: a section filled in halfway is a mistake to say at the start, not a member's failed link.</summary>
public sealed class DiscordOAuthOptionsValidator : IValidateOptions<DiscordOAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, DiscordOAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options is { ClientId: "", ClientSecret: "", RedirectUri: "" })
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (options.ClientId.Length == 0)
        {
            failures.Add("Discord: 'ClientId' is required when the section is there.");
        }

        if (options.ClientSecret.Length == 0)
        {
            failures.Add("Discord: 'ClientSecret' is required when the section is there.");
        }

        var callback = Uri.TryCreate(options.RedirectUri, UriKind.Absolute, out var redirect) ? redirect : null;
        if (callback is null
            || (callback.Scheme != Uri.UriSchemeHttp && callback.Scheme != Uri.UriSchemeHttps)
            || !string.Equals(callback.AbsolutePath, DiscordLinkAuthentication.CallbackPath, StringComparison.Ordinal))
        {
            failures.Add($"Discord: 'RedirectUri' must be an absolute http or https URL ending with {DiscordLinkAuthentication.CallbackPath}.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
