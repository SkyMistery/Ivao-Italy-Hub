using System.Globalization;
using System.Text;

namespace IvaoHub.Core.Services;

/// <summary>
/// Writes <c>diagnostics/startup.txt</c> at every start: which package is running, which migrations
/// it applied, which modules are on. It is the first thing to read after an FTP deploy, and it
/// never contains a secret (design M0 section 2.4).
/// </summary>
/// <remarks>
/// The folder is denied by the web server: it is read over FTP, never over HTTP.
/// </remarks>
public static class StartupDiagnostics
{
    public const string FileName = "startup.txt";

    public static async Task WriteAsync(
        HubPaths paths,
        BuildInfo build,
        string environment,
        string divisionCode,
        IReadOnlyList<string> appliedMigrations,
        IReadOnlyList<string> enabledModules,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(appliedMigrations);
        ArgumentNullException.ThrowIfNull(enabledModules);

        var report = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"started at    {DateTime.UtcNow:O}\n")
            .Append(CultureInfo.InvariantCulture, $"version       {build.Version}\n")
            .Append(CultureInfo.InvariantCulture, $"commit        {build.Commit}\n")
            .Append(CultureInfo.InvariantCulture, $"built at      {build.BuiltAt:O}\n")
            .Append(CultureInfo.InvariantCulture, $"runtime       {build.Dotnet}\n")
            .Append(CultureInfo.InvariantCulture, $"environment   {environment}\n")
            .Append(CultureInfo.InvariantCulture, $"division      {divisionCode}\n")
            .Append(CultureInfo.InvariantCulture, $"root          {paths.Root}\n")
            .Append(CultureInfo.InvariantCulture, $"migrations    {Join(appliedMigrations, "none applied, already up to date")}\n")
            .Append(CultureInfo.InvariantCulture, $"modules       {Join(enabledModules, "none yet")}\n")
            .ToString();

        Directory.CreateDirectory(paths.Diagnostics);
        await File.WriteAllTextAsync(Path.Combine(paths.Diagnostics, FileName), report, cancellationToken);
    }

    private static string Join(IReadOnlyList<string> values, string empty) =>
        values.Count == 0 ? empty : string.Join(", ", values);
}
