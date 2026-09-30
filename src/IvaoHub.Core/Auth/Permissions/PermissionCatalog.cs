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

    // Learnt once, when the hub starts (LearnAreasWithAFir); none until then, which refuses every grant to a FIR team.
    private volatile IReadOnlySet<string> _areasWithAFir = new HashSet<string>(StringComparer.Ordinal);

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
                // The lists narrow in SQL by department and know nothing of whom a row is assigned to: the area's View
                // would show every row there and refuse most of them one by one (M3, A3b). The catalogue can tell the
                // area's View apart, and only that one: whether any other permission reads is not written anywhere.
                throw new InvalidOperationException(
                    $"'{descriptor.Name}' is the View permission of its area, and the area's View permission is never "
                    + "OnlyForAssignee: the lists narrow by department and would show the rows anyway.");
            }

            if (string.Equals(EditOf(descriptor.Name), descriptor.Name, StringComparison.Ordinal))
            {
                // The area's Edit is what a marked permission falls back on, on a row not assigned to the writer: marked, it
                // would fall back on itself, and whoever writes the whole area would write only their own rows in the handler
                // and every row in the guard (note 2026-09-30-il-controllo-all-avvio-rinforzato).
                throw new InvalidOperationException(
                    $"'{descriptor.Name}' is the Edit permission of its area, and the area's Edit permission is never "
                    + "OnlyForAssignee: it is what a marked permission is worth on a row not assigned to the writer.");
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
    /// Remembers the permission areas that have an entity which says its FIR (<see cref="IHasFir"/>): the only areas a grant to
    /// the team of a FIR may name a permission of (M3, A11a, note 2026-09-27-i-capi-fir-sul-loro-fir §3.1). Anywhere else such a
    /// grant would reach no row, and answer "yes" only to the question asked without one.
    /// <para>The hub calls it when it starts, from the model of every context, next to <see cref="VerifyAlternatives"/>; the
    /// area of an entity is the one the write guard asks <c>Edit</c> of.</para>
    /// </summary>
    public void LearnAreasWithAFir(IEnumerable<string> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        _areasWithAFir = areas.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Whether a grant to the team of a FIR may name this permission: its area has rows that say their FIR.</summary>
    public bool IsOfAnAreaWithAFir(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var dot = name.IndexOf('.', StringComparison.Ordinal);
        return dot > 0 && _areasWithAFir.Contains(name[..dot]);
    }

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
    /// <para>And three more ways a marked permission would let the handler and the guard answer differently on the same row
    /// (note 2026-09-30-il-controllo-all-avvio-rinforzato): its <c>Edit</c> is not the one of the entity's area, which the
    /// handler falls back on and the guard does not; the entity has no department (<see cref="IOwnedByDepartment"/>), which
    /// the guard asks the alternative on; the entity says whom a row is about (<see cref="IHasStakeholder"/>) and the permission
    /// is not <c>DeniedToStakeholder</c>, when the guard keeps the stakeholder out of every alternative.</para>
    /// <para>The hub calls it when it starts, on the model of every context, so that a wrong declaration stops the start
    /// rather than turning into a 403 nobody can explain. <paramref name="areaOf"/> is the area the guard asks <c>Edit</c> of,
    /// which the hub works out from the context; without it only an area declared with <see cref="PermissionAreaAttribute"/>
    /// is checked.</para>
    /// </summary>
    public void VerifyAlternatives(IEnumerable<Type> entities, Func<Type, string?>? areaOf = null)
    {
        ArgumentNullException.ThrowIfNull(entities);
        areaOf ??= DeclaredAreaOf;

        var wrong = entities
            .Distinct()
            .SelectMany(entity => entity.GetCustomAttributes<AlsoWrittenWithAttribute>(inherit: false)
                .SelectMany(alternative => WhatIsWrong(entity, areaOf(entity), alternative)))
            .ToArray();

        if (wrong.Length > 0)
        {
            throw new InvalidOperationException(string.Join(" ", wrong));
        }
    }

    private IEnumerable<string> WhatIsWrong(Type entity, string? area, AlsoWrittenWithAttribute alternative)
    {
        var permission = alternative.Permission;

        if (!IsOnlyForAssignee(permission))
        {
            if (alternative.AlsoOnDeletion)
            {
                yield return $"{entity.Name} lets {permission} delete (AlsoOnDeletion), which only a permission that reaches "
                    + "the rows assigned to the writer may do (OnlyForAssignee).";
            }

            yield break;
        }

        if (!typeof(IHasAssignee).IsAssignableFrom(entity))
        {
            yield return $"{entity.Name} is also written with {permission}, which reaches only the rows assigned to the "
                + "writer, but it does not say whom a row is assigned to (IHasAssignee).";
        }

        if (area is not null && !string.Equals(EditOf(permission), $"{area}.Edit", StringComparison.Ordinal))
        {
            yield return $"{entity.Name} is also written with {permission}, which on a row not assigned to the writer is "
                + $"worth {EditOf(permission) ?? "nothing"}, but the entity's area is written with {area}.Edit.";
        }

        if (!typeof(IOwnedByDepartment).IsAssignableFrom(entity))
        {
            yield return $"{entity.Name} is also written with {permission}, which reaches only the rows assigned to the "
                + "writer, but it has no department to ask it on (IOwnedByDepartment).";
        }

        if (typeof(IHasStakeholder).IsAssignableFrom(entity) && !IsDeniedToStakeholder(permission))
        {
            yield return $"{entity.Name} says whom a row is about (IHasStakeholder) and is also written with {permission}, "
                + "which is not DeniedToStakeholder: the endpoint would let the stakeholder in and the save would not.";
        }
    }

    private static string? DeclaredAreaOf(Type entity) => entity.GetCustomAttribute<PermissionAreaAttribute>(inherit: false)?.Area;

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
