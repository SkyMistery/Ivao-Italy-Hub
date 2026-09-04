namespace IvaoHub.Core.Modules;

/// <summary>
/// Every dashboard tile this installation knows: the core's own plus the ones the modules declare.
/// Composed from the container, exactly like <c>BlockRegistry</c>, so that a module registers a
/// descriptor and nothing else has to be told its name (design M0 section 6.3).
/// <para>What a tile looks like lives in TypeScript, like a block: this is the envelope side of it,
/// published in <c>/api/me</c> so the dashboard composes what it is handed instead of holding a
/// list of tiles inside a screen.</para>
/// </summary>
public sealed class WidgetRegistry
{
    public WidgetRegistry(IEnumerable<WidgetDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var byKey = new Dictionary<string, WidgetDescriptor>(StringComparer.Ordinal);

        foreach (var descriptor in descriptors)
        {
            if (!byKey.TryAdd(descriptor.Key, descriptor))
            {
                throw new InvalidOperationException($"The widget '{descriptor.Key}' is registered twice.");
            }
        }

        All = [.. byKey.Values.OrderBy(descriptor => descriptor.Key, StringComparer.Ordinal)];
    }

    /// <summary>Every tile, in a stable order so the bootstrap payload does not churn.</summary>
    public IReadOnlyList<WidgetDescriptor> All { get; }
}

/// <summary>
/// The tiles of the core. One, in M0: it is the mechanism that had to be proved, not a library of
/// tiles (design M0 section 6.3). Modules bring their own from M2.
/// </summary>
public static class CoreWidgets
{
    /// <summary>Who is signed in and what the hub knows about them.</summary>
    public const string Welcome = "welcome";

    public static readonly IReadOnlyList<WidgetDescriptor> All =
    [
        new(Welcome, Department: null, TitleKey: "widgets.welcome.title", Sizes: ["full"]),
    ];
}
