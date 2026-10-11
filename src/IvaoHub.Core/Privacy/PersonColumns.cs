using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IvaoHub.Core.Privacy;

/// <summary>
/// Which columns name a person, told by their name: an integer called <c>Vid</c>, <c>…Vid</c> or <c>…By</c> (<c>Vid</c>,
/// <c>DecidedByVid</c>, <c>CreatedBy</c>, <c>HandledBy</c>). It is the convention every table of the hub already follows, and
/// the erasure of a person's data leans on it (note <c>2026-09-25-la-cancellazione-dei-dati-di-una-persona</c> §3): each of
/// these columns that holds the person becomes their pseudonym, in the core and in every module, with nothing for a module
/// to write. A column of a person named otherwise would be missed — which is why a new one follows the convention.
/// <para>Lists of VIDs are the one shape a name cannot tell from an integer, and there is one: the participants of a thread,
/// which the core treats by hand.</para>
/// </summary>
public static class PersonColumns
{
    /// <summary>The columns that hold a list of VIDs as JSON, by property name.</summary>
    public static readonly IReadOnlySet<string> VidLists = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ParticipantsJson",
    };

    /// <summary>Whether a property of an entity is the VID of a person.</summary>
    public static bool IsVid(IReadOnlyProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return (property.ClrType == typeof(int) || property.ClrType == typeof(int?)) && IsVidName(property.Name);
    }

    /// <summary>Whether a property of an entity names people, alone or in a list: what the erasure may change without it being a change of the row.</summary>
    public static bool NamesPeople(IReadOnlyProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return IsVid(property) || VidLists.Contains(property.Name);
    }

    /// <summary>
    /// The same rule on a bare name, for the JSON of an audit row — whose properties are the entity's, as named in C#
    /// (<c>"BookerVid"</c>) — and for other JSON the core reads, which may be camel case.
    /// </summary>
    public static bool IsVidName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Equals("Vid", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Vid", StringComparison.Ordinal)
            || (name.Length > 2 && name.EndsWith("By", StringComparison.Ordinal));
    }

    /// <summary>
    /// The columns of an entity marked as a person's data (<see cref="ErasedWithAttribute"/>), with the person column each names,
    /// grouped by it. Refuses a mark that does not name a person column of the same entity: a mark that names nothing would empty
    /// nothing, without a word.
    /// </summary>
    public static IReadOnlyList<(IReadOnlyProperty Person, IReadOnlyList<IReadOnlyProperty> Columns)> ErasedWith(IReadOnlyEntityType entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var marked = entity.GetProperties()
            .Select(property => (Property: property,
                Mark: property.PropertyInfo?.GetCustomAttributes(typeof(ErasedWithAttribute), inherit: false)
                    .OfType<ErasedWithAttribute>()
                    .FirstOrDefault()))
            .Where(column => column.Mark is not null)
            .ToList();

        var groups = new List<(IReadOnlyProperty, IReadOnlyList<IReadOnlyProperty>)>();
        foreach (var group in marked.GroupBy(column => column.Mark!.Person, StringComparer.Ordinal))
        {
            var person = entity.FindProperty(group.Key);
            if (person is null || !IsVid(person))
            {
                throw new InvalidOperationException(
                    $"{entity.ClrType.Name}.{group.First().Property.Name} is marked as the data of '{group.Key}', which is not an "
                    + "integer column of the same entity that names a person (Vid, …Vid or …By).");
            }

            groups.Add((person, [.. group.Select(column => column.Property)]));
        }

        return groups;
    }

    /// <summary>
    /// Whether the context is the one that rewrites this table: the core's context its own types, a module's context the
    /// module's. A module context also maps some of the core's (the projections, the threads), which the core's context
    /// rewrites once. Asked of the type rather than of the migrations, which the runtime model does not keep.
    /// </summary>
    public static bool RewrittenBy(DbContext context, IReadOnlyEntityType entity)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entity);

        var core = entity.ClrType.Assembly == typeof(HubDbContext).Assembly;
        return !entity.IsOwned()
            && entity.FindPrimaryKey() is not null
            && entity.GetTableName() is not null
            && entity.ClrType != typeof(AuditLogEntry)
            && core == (context is HubDbContext);
    }
}
