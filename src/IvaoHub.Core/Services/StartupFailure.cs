using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace IvaoHub.Core.Services;

/// <summary>
/// Writes <c>diagnostics/startup-error.txt</c> when the application stops before it has started: the
/// reason a start failed, where an installation served by FTP alone can read it (note
/// 2026-09-27-l-avvio-da-qualunque-cartella). Standard output keeps the message as before; this is a
/// second copy, for whoever cannot read the host's log.
/// </summary>
/// <remarks>
/// It reads no configuration and writes none: only the build, the environment, the paths and the
/// exception. An exception's message may still quote a value, so every value that could be a secret
/// is replaced before the file is written (<see cref="SecretValues"/>). The next start that succeeds
/// deletes the file, and writes <c>startup.txt</c> as always.
/// </remarks>
public static class StartupFailure
{
    public const string FileName = "startup-error.txt";

    public const string Redacted = "[redacted]";

    /// <summary>
    /// Shorter values are left alone: they are flags and numbers ("true", "3306"), and replacing them would garble the
    /// text. The price: a secret this short, a database password of five characters say, would appear if a message
    /// quoted it.
    /// </summary>
    private const int ShortestSecret = 6;

    /// <summary>What goes into the file, with every one of <paramref name="secrets"/> taken out.</summary>
    public static string Report(
        HubPaths paths,
        BuildInfo build,
        string environment,
        Exception exception,
        DateTime failedAtUtc,
        string workingDirectory,
        string applicationFolder,
        IEnumerable<string> secrets)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(secrets);

        var report = new StringBuilder()
            .Append("THE APPLICATION DID NOT START. The reason is at the bottom of this file.\n")
            .Append("This file is deleted by the next start that succeeds.\n\n")
            .Append(CultureInfo.InvariantCulture, $"failed at     {failedAtUtc:O}\n")
            .Append(CultureInfo.InvariantCulture, $"version       {build.Version}\n")
            .Append(CultureInfo.InvariantCulture, $"commit        {build.Commit}\n")
            .Append(CultureInfo.InvariantCulture, $"runtime       {build.Dotnet}\n")
            .Append(CultureInfo.InvariantCulture, $"environment   {environment}\n")
            .Append(CultureInfo.InvariantCulture, $"root          {paths.Root} ({paths.SourceDescription})\n")
            .Append(CultureInfo.InvariantCulture, $"working dir   {workingDirectory}\n")
            .Append(CultureInfo.InvariantCulture, $"application   {applicationFolder}\n\n")
            .Append(CultureInfo.InvariantCulture, $"reason        {Describe(exception)}\n");

        // The one that says what happened is often the innermost: "a transient failure" from EF, and
        // under it "unable to connect to any of the specified hosts".
        var cause = exception.GetBaseException();
        if (!ReferenceEquals(cause, exception))
        {
            report.Append(CultureInfo.InvariantCulture, $"cause         {Describe(cause)}\n");
        }

        var text = report
            .Append('\n')
            .Append(exception)
            .Append('\n')
            .ToString();

        // Longest first, so that a secret containing another is replaced whole.
        foreach (var secret in secrets
                     .Where(value => value.Length >= ShortestSecret)
                     .Distinct(StringComparer.Ordinal)
                     .OrderByDescending(value => value.Length))
        {
            text = text.Replace(secret, Redacted, StringComparison.Ordinal);
        }

        return text;
    }

    private static string Describe(Exception exception) => $"{exception.GetType().FullName}: {exception.Message}";

    /// <summary>
    /// Writes the report under the root's <c>diagnostics/</c>. Never throws: it runs while the process
    /// is already failing, and a second exception would only hide the first.
    /// </summary>
    public static string? Write(HubPaths paths, string report)
    {
        ArgumentNullException.ThrowIfNull(paths);

        try
        {
            Directory.CreateDirectory(paths.Diagnostics);
            var file = Path.Combine(paths.Diagnostics, FileName);
            File.WriteAllText(file, report);
            return file;
        }
#pragma warning disable CA1031 // Whatever stops the write, the start has failed already and says so on standard output.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }

    /// <summary>Removes the report of an earlier failure, once a start has succeeded.</summary>
    public static void Clear(HubPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var file = Path.Combine(paths.Diagnostics, FileName);
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// The values a report must never quote: every value in the files of <c>secrets/</c> and in the
    /// OAuth file, read again here because the failure may have come before they were loaded, and
    /// every value of <paramref name="configuration"/> whose key names a connection string, a
    /// password, a secret, a token or a key, wherever it came from (an environment variable, say). A
    /// file that cannot be read gives nothing, and is not the reason reported.
    /// </summary>
    public static IReadOnlyCollection<string> SecretValues(HubPaths paths, IConfiguration? configuration)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var values = new List<string>();
        foreach (var file in paths.SecretFiles().Append(paths.OAuthFile))
        {
            try
            {
                values.AddRange(Values(new ConfigurationBuilder().AddJsonFile(file, optional: true).Build()));
            }
#pragma warning disable CA1031 // An unreadable file is skipped: it is usually the very failure being reported.
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }

        if (configuration is not null)
        {
            values.AddRange(Values(configuration, IsSecretKey));
        }

        return values;
    }

    /// <summary>Words that make a key a secret wherever it comes from, the environment included.</summary>
    private static readonly string[] SecretWords = ["ConnectionStrings", "Password", "Secret", "Token", "Key"];

    private static bool IsSecretKey(string key) =>
        SecretWords.Any(word => key.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> Values(IConfiguration configuration, Func<string, bool>? keep = null) =>
        configuration.AsEnumerable()
            .Where(pair => !string.IsNullOrEmpty(pair.Value) && (keep is null || keep(pair.Key)))
            .Select(pair => pair.Value!);
}
