namespace IvaoHub.Core.Services;

/// <summary>
/// Where the files that are not code live: <c>config/</c>, <c>locales/</c>, <c>segreti/</c>,
/// <c>hub-keys/</c>, <c>logs/</c>, <c>diagnostica/</c>.
/// </summary>
/// <remarks>
/// In production they sit next to the application, which is also the content root. During
/// development the content root is the web project while those folders are at the root of the
/// repository, so the root is found by walking up until <c>config/division.json</c> appears.
/// <c>IVAOHUB_ROOT</c> overrides everything, which is what the tests use.
/// </remarks>
public sealed class HubPaths
{
    /// <summary>Environment variable that pins the root explicitly.</summary>
    public const string RootVariable = "IVAOHUB_ROOT";

    private const string Marker = "config/division.json";
    private const int MaxLevels = 6;

    private HubPaths(string root) => Root = root;

    public string Root { get; }

    public string Config => Path.Combine(Root, "config");
    public string Locales => Path.Combine(Root, "locales");
    public string Secrets => Path.Combine(Root, "segreti");
    public string DataProtectionKeys => Path.Combine(Root, "hub-keys");
    public string Logs => Path.Combine(Root, "logs");
    public string Diagnostics => Path.Combine(Root, "diagnostica");

    public string DivisionFile => Path.Combine(Config, "division.json");
    public string OAuthFile => Path.Combine(Config, "ivao-oauth.json");

    public static HubPaths Resolve(string contentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        var pinned = Environment.GetEnvironmentVariable(RootVariable);
        if (!string.IsNullOrWhiteSpace(pinned))
        {
            return new HubPaths(Path.GetFullPath(pinned));
        }

        var directory = new DirectoryInfo(Path.GetFullPath(contentRoot));
        for (var level = 0; level < MaxLevels && directory is not null; level++)
        {
            if (File.Exists(Path.Combine(directory.FullName, Marker)))
            {
                return new HubPaths(directory.FullName);
            }

            directory = directory.Parent;
        }

        return new HubPaths(Path.GetFullPath(contentRoot));
    }
}
