using System.Reflection;
using IvaoHub.Core.Division;

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
                    + "permissions after itself, for example 'Events.Manage'.");
            }
        }

        foreach (var descriptor in _byName.Values.Where(descriptor => descriptor.OnlyForAssignee))
        {
            if (string.Equals(ViewOf(descriptor.Name), descriptor.Name, StringComparison.Ordinal))
            {
                // The lists narrow in SQL by department and know nothing of whom a row is assigned to: a permission that
                // reads would show every row there and refuse most of them one by one (M3, A3b).
                throw new InvalidOperationException(
                    $"The permission '{descriptor.Name}' reads, and a permission that reads is never OnlyForAssignee: "
                    + "the lists narrow by department and would show the rows anyway.");
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

    /// <summary>
    /// Whether the person a row is about may not use this permission on it. Unknown names answer
    /// false: a permission the catalogue does not know is refused earlier, by the policy provider.
    /// </summary>
    public bool IsDeniedToStakeholder(string name) =>
        _byName.TryGetValue(name, out var found) && found.DeniedToStakeholder;

    public bool IsGlobal(string name) => _byName.TryGetValue(name, out var found) && found.IsGlobal;

    /// <summary>
    /// Whether this permission reaches a row only for the member the row is assigned to (M3, A3b). Unknown names answer
    /// false, as for <see cref="IsDeniedToStakeholder"/>.
    /// </summary>
    public bool IsOnlyForAssignee(string name) =>
        _byName.TryGetValue(name, out var found) && found.OnlyForAssignee;

    /// <summary>
    /// The view permission of the same area, so that "Edit implies View" is decided in one place.
    /// Null when the name has no matching view permission.
    /// </summary>
    public string? ViewOf(string name) => OfTheSameArea(name, ".View");

    /// <summary>
    /// The edit permission of the same area, the one that writes every row of it: what a permission marked
    /// <c>OnlyForAssignee</c> is worth on a row not assigned to the asker (M3, A3b). Null when the area has none.
    /// </summary>
    public string? EditOf(string name) => OfTheSameArea(name, ".Edit");

    /// <summary>
    /// Refuses what an entity declares besides <c>Edit</c> (<see cref="AlsoWrittenWithAttribute"/>) that the interceptor's guard
    /// could not honour as written (M3, A3b, note 2026-09-26-le-righe-affidate-a-chi-scrive §3.5-bis): <c>AlsoOnDeletion</c> on
    /// a permission not marked <c>OnlyForAssignee</c>, which would delete nothing, and a marked permission on an entity that
    /// does not say whom a row is assigned to (<see cref="IHasAssignee"/>), which would be worth <c>Edit</c> and nothing more.
    /// <para>The hub calls it when it starts, on the model of every context, so that a wrong declaration stops the start
    /// rather than turning into a 403 nobody can explain.</para>
    /// </summary>
    public void VerifyAlternatives(IEnumerable<Type> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var wrong = entities
            .Distinct()
            .SelectMany(entity => entity.GetCustomAttributes<AlsoWrittenWithAttribute>(inherit: false)
                .Select(alternative => WhatIsWrong(entity, alternative)))
            .OfType<string>()
            .ToArray();

        if (wrong.Length > 0)
        {
            throw new InvalidOperationException(string.Join(" ", wrong));
        }
    }

    private string? WhatIsWrong(Type entity, AlsoWrittenWithAttribute alternative)
    {
        if (IsOnlyForAssignee(alternative.Permission))
        {
            return typeof(IHasAssignee).IsAssignableFrom(entity)
                ? null
                : $"{entity.Name} is also written with {alternative.Permission}, which reaches only the rows assigned to the "
                    + "writer, but it does not say whom a row is assigned to (IHasAssignee).";
        }

        return alternative.AlsoOnDeletion
            ? $"{entity.Name} lets {alternative.Permission} delete (AlsoOnDeletion), which only a permission that reaches "
                + "the rows assigned to the writer may do (OnlyForAssignee)."
            : null;
    }

    private string? OfTheSameArea(string name, string action)
    {
        ArgumentNullException.ThrowIfNull(name);

        var dot = name.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0)
        {
            return null;
        }

        var sibling = string.Concat(name.AsSpan(0, dot), action);
        return IsKnown(sibling) ? sibling : null;
    }
}
