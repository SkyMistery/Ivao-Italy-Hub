using System.Reflection;
using System.Runtime.InteropServices;

namespace IvaoHub.Core.Services;

/// <summary>
/// The stamp exposed by <c>/api/version</c>, so that after an FTP deploy anybody can tell which
/// package is actually running (plan section 11.3).
/// </summary>
public sealed record BuildInfo(string Version, string Commit, DateTime BuiltAt, string Dotnet)
{
    private const string Unknown = "unknown";

    public static BuildInfo FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(attribute => attribute.Value is not null)
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value!, StringComparer.OrdinalIgnoreCase);

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = informational?.Split('+')[0] ?? assembly.GetName().Version?.ToString() ?? Unknown;

        var commit = metadata.GetValueOrDefault("CommitHash");
        if (string.IsNullOrWhiteSpace(commit))
        {
            var plus = informational?.IndexOf('+', StringComparison.Ordinal) ?? -1;
            commit = plus >= 0 ? informational![(plus + 1)..] : Unknown;
        }

        var builtAt = metadata.TryGetValue("BuildTimestamp", out var stamp)
            && DateTime.TryParse(stamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed.ToUniversalTime()
                : LastWriteTime(assembly);

        return new BuildInfo(version, commit, builtAt, RuntimeInformation.FrameworkDescription);
    }

    private static DateTime LastWriteTime(Assembly assembly)
    {
        var location = assembly.Location;
        return string.IsNullOrEmpty(location) ? DateTime.UnixEpoch : File.GetLastWriteTimeUtc(location);
    }
}
