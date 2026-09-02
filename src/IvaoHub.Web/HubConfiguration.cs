using IvaoHub.Core.Services;

namespace IvaoHub.Web;

/// <summary>Reading the configuration files of an installation, in one place.</summary>
internal static class HubConfiguration
{
    /// <summary>
    /// Every <c>*.json</c> under <c>segreti/</c>, in a stable order. The folder is never in the
    /// repository and the web server denies access to it (plan section 11.3).
    /// </summary>
    public static IEnumerable<string> SecretFiles(HubPaths paths)
    {
        if (!Directory.Exists(paths.Secrets))
        {
            return [];
        }

        return Directory.EnumerateFiles(paths.Secrets, "*.json").OrderBy(file => file, StringComparer.Ordinal);
    }

    /// <summary>
    /// The division file is loaded on its own so that its keys never mix with the settings of the
    /// application. Missing or unreadable, the application does not start.
    /// </summary>
    public static IConfiguration DivisionFile(HubPaths paths)
    {
        if (!File.Exists(paths.DivisionFile))
        {
            throw new InvalidOperationException(
                $"The division file is missing: {paths.DivisionFile}. "
                + "Copy config/division.example.json to config/division.json and fill it in.");
        }

        return new ConfigurationBuilder().AddJsonFile(paths.DivisionFile, optional: false).Build();
    }

    /// <summary>
    /// In production the host list must be explicit: the OIDC redirect URI is built from the Host
    /// header, so a wildcard would let a forged header send the login somewhere else.
    /// </summary>
    public static void RequireAllowedHosts(IConfiguration configuration)
    {
        var allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Split(';').Any(host => host.Trim() == "*"))
        {
            throw new InvalidOperationException(
                "'AllowedHosts' must list the real host names in production, without '*'.");
        }
    }
}
