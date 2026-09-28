namespace IvaoHub.Core.Services;

/// <summary>
/// Where the files that are not code live: <c>config/</c>, <c>locales/</c>, <c>secrets/</c>,
/// <c>hub-keys/</c>, <c>logs/</c>, <c>diagnostics/</c>, <c>seed/</c>, <c>media/</c>, <c>tiles/</c>.
/// </summary>
/// <remarks>
/// In production they sit next to the application, and an application folder holding
/// <c>config/division.json</c> is the root. During development the content root is the web project while
/// those folders are at the root of the repository, so otherwise the root is found by walking up until
/// <c>config/division.json</c> appears: from the content root first, then from the folder of the
/// application, because a host may start the process from any working directory (notes
/// 2026-09-27-l-avvio-da-qualunque-cartella, 2026-09-28-un-avvio-piu-veloce). <c>IVAOHUB_ROOT</c> overrides
/// everything, which is what the tests use.
/// </remarks>
public sealed class HubPaths
{
    /// <summary>Environment variable that pins the root explicitly.</summary>
    public const string RootVariable = "IVAOHUB_ROOT";

    private const string Marker = "config/division.json";
    private const int MaxLevels = 6;

    private HubPaths(string root, HubRootSource source)
    {
        // AppContext.BaseDirectory ends with a separator; a root is written without one.
        Root = Path.TrimEndingDirectorySeparator(root);
        Source = source;
    }

    public string Root { get; }

    /// <summary>How the root was found, written into the diagnostics so that a wrong one can be explained.</summary>
    public HubRootSource Source { get; }

    /// <summary><see cref="Source"/> in words, for the diagnostics files.</summary>
    public string SourceDescription => Source switch
    {
        HubRootSource.Pinned => $"set by {RootVariable}",
        HubRootSource.ContentRoot => "found from the content root",
        HubRootSource.ApplicationFolder => "found from the folder of the application",
        _ => $"not found: no {Marker} above the content root nor above the folder of the application",
    };

    public string Config => Path.Combine(Root, "config");
    public string Locales => Path.Combine(Root, "locales");

    /// <summary>What a fresh installation starts with: the system content templates.</summary>
    public string Seed => Path.Combine(Root, "seed");

    /// <summary>
    /// Uploaded files. On disk and never in a <c>longblob</c> (plan section 11.3), and outside the
    /// package so that a deployment does not overwrite them.
    /// </summary>
    public string Media => Path.Combine(Root, "media");
    /// <summary>
    /// The base map of the tours, one PMTiles archive of the world (note 2026-09-15-la-mappa). Next to the uploads
    /// and outside the package for the same reason: it belongs to an installation, is put there over FTP, and a
    /// deployment must not overwrite it. An installation without it draws maps on a neutral ground.
    /// </summary>
    public string Tiles => Path.Combine(Root, "tiles");

    public string Secrets => Path.Combine(Root, "secrets");
    public string DataProtectionKeys => Path.Combine(Root, "hub-keys");
    public string Logs => Path.Combine(Root, "logs");
    public string Diagnostics => Path.Combine(Root, "diagnostics");

    public string DivisionFile => Path.Combine(Config, "division.json");
    public string OAuthFile => Path.Combine(Config, "ivao-oauth.json");

    /// <summary>
    /// The headers every response carries. Read by the host **and** by Vite's preview server, so
    /// that the smoke suite runs under the real policy; it is in the repository because it is not a
    /// secret and an installation that edits it is making a decision, not filling in a blank.
    /// </summary>
    public string SecurityFile => Path.Combine(Config, "security.json");

    /// <summary>
    /// Every <c>*.json</c> under <c>secrets/</c>, in a stable order. The folder is never in the
    /// repository and the web server denies access to it (plan section 11.3).
    /// </summary>
    public IEnumerable<string> SecretFiles() => Directory.Exists(Secrets)
        ? Directory.EnumerateFiles(Secrets, "*.json").OrderBy(file => file, StringComparer.Ordinal)
        : [];

    public static HubPaths Resolve(string contentRoot) => Resolve(contentRoot, applicationFolder: null);

    /// <summary>
    /// The root: <see cref="RootVariable"/> when set; else <paramref name="applicationFolder"/> itself when it holds the
    /// marker; else the first folder holding the marker above <paramref name="contentRoot"/>, then above
    /// <paramref name="applicationFolder"/>; else the application folder, which is where an installation is told to put
    /// the division file.
    /// </summary>
    /// <remarks>
    /// The application's own division file comes before the walk from the content root because the walk climbs: an
    /// installation started from its own folder, with another installation's <c>config/division.json</c> a few levels
    /// higher in the same FTP tree, took that one and said nothing. During development the application folder is
    /// <c>bin/</c>, which never holds a division file, so the walk from the web project finds the repository as before.
    /// </remarks>
    public static HubPaths Resolve(string contentRoot, string? applicationFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        var pinned = Environment.GetEnvironmentVariable(RootVariable);
        if (!string.IsNullOrWhiteSpace(pinned))
        {
            return new HubPaths(Path.GetFullPath(pinned), HubRootSource.Pinned);
        }

        if (!string.IsNullOrWhiteSpace(applicationFolder) && HasMarker(applicationFolder))
        {
            return new HubPaths(Path.GetFullPath(applicationFolder), HubRootSource.ApplicationFolder);
        }

        if (FindMarker(contentRoot) is { } fromContentRoot)
        {
            return new HubPaths(fromContentRoot, HubRootSource.ContentRoot);
        }

        if (!string.IsNullOrWhiteSpace(applicationFolder) && FindMarker(applicationFolder) is { } fromApplication)
        {
            return new HubPaths(fromApplication, HubRootSource.ApplicationFolder);
        }

        return new HubPaths(
            Path.GetFullPath(string.IsNullOrWhiteSpace(applicationFolder) ? contentRoot : applicationFolder),
            HubRootSource.NotFound);
    }

    private static string? FindMarker(string start)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(start));
        for (var level = 0; level < MaxLevels && directory is not null; level++)
        {
            if (HasMarker(directory.FullName))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static bool HasMarker(string folder) => File.Exists(Path.Combine(Path.GetFullPath(folder), Marker));
}

/// <summary>How <see cref="HubPaths.Resolve(string, string?)"/> found the root.</summary>
public enum HubRootSource
{
    Pinned,
    ContentRoot,
    ApplicationFolder,
    NotFound,
}
