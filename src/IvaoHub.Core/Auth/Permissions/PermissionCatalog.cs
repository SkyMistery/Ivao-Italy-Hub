namespace IvaoHub.Core.Auth.Permissions;

/// <summary>
/// Every permission this installation knows: the ones of the core plus the ones the modules
/// declare in <c>IModule.Permissions</c>. It is the one thing that answers "is this a permission?",
/// and it is asked by the policy provider, by the calculator of effective permissions and by the
/// validator of a grant — so that a module permission is a permission everywhere or nowhere
/// (design M0 sections 3.7 and 6.1).
/// <para>An instance and not a static list, because what a fork installs is not known at compile
/// time. <see cref="Core"/> is the catalogue of a hub with no modules at all, which is what the
/// pieces that have no container to ask — a unit test, a design time tool — use.</para>
/// </summary>
public sealed class PermissionCatalog
{
    private readonly Dictionary<string, PermissionDescriptor> _byName;

    public PermissionCatalog(IEnumerable<PermissionDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        _byName = new Dictionary<string, PermissionDescriptor>(StringComparer.Ordinal);

        foreach (var descriptor in descriptors)
        {
            if (!_byName.TryAdd(descriptor.Name, descriptor))
            {
                // Two modules claiming one name would each think they own the policy, and which of
                // the two was scoped to a department would depend on the order of a list.
                throw new InvalidOperationException(
                    $"The permission '{descriptor.Name}' is declared twice. A module names its "
                    + "permissions after itself, for example 'Atc.View'.");
            }
        }

        All = [.. _byName.Values];
        Departmental = [.. All.Where(permission => !permission.IsGlobal).Select(permission => permission.Name)];
        Global = [.. All.Where(permission => permission.IsGlobal).Select(permission => permission.Name)];
    }

    /// <summary>The catalogue of a hub with no modules: the permissions of the core, and those only.</summary>
    public static PermissionCatalog Core { get; } = new(CorePermissions.All);

    /// <summary>Every permission, core first and then the modules in the order they are listed.</summary>
    public IReadOnlyList<PermissionDescriptor> All { get; }

    /// <summary>The ones scoped to a department, the ones a coordinator holds on their own.</summary>
    public IReadOnlyList<string> Departmental { get; }

    /// <summary>The ones with no department, and that a grant may therefore never confer.</summary>
    public IReadOnlyList<string> Global { get; }

    public bool IsKnown(string? name) => name is not null && _byName.ContainsKey(name);

    public bool IsGlobal(string name) => _byName.TryGetValue(name, out var found) && found.IsGlobal;

    /// <summary>
    /// The view permission of the same area, so that "Edit implies View" is decided in one place.
    /// Null when the name has no matching view permission.
    /// </summary>
    public string? ViewOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var dot = name.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0)
        {
            return null;
        }

        var view = string.Concat(name.AsSpan(0, dot), ".View");
        return IsKnown(view) ? view : null;
    }
}
