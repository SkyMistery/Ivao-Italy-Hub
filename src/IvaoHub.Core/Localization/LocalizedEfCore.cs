using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IvaoHub.Core.Localization;

/// <summary>
/// Stores a <see cref="Localized{T}"/> as a MariaDB <c>json</c> object, <c>{ "en": …, "it": … }</c>.
/// Registered once for the whole model in <c>HubDbContext.ConfigureConventions</c>: no entity ever
/// configures its own converter.
/// </summary>
public sealed class LocalizedConverter<T> : ValueConverter<Localized<T>, string>
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public LocalizedConverter()
        : base(
            value => Serialize(value),
            json => Deserialize(json))
    {
    }

    private static string Serialize(Localized<T> value) =>
        JsonSerializer.Serialize(value.ToDictionary(pair => pair.Key, pair => pair.Value), SerializerOptions);

    private static Localized<T> Deserialize(string json) =>
        string.IsNullOrWhiteSpace(json)
            ? Localized<T>.Empty
            : new Localized<T>(JsonSerializer.Deserialize<Dictionary<string, T>>(json, SerializerOptions) ?? []);
}

/// <summary>Value comparer for change tracking: a localized field is compared by content.</summary>
public sealed class LocalizedComparer<T> : ValueComparer<Localized<T>>
{
    public LocalizedComparer()
        : base(
            (left, right) => left == null ? right == null : right != null && left.Equals(right),
            value => value.GetHashCode(),
            value => new Localized<T>(value))
    {
    }
}

/// <summary>
/// Appends <c>_i18n</c> to every column that holds a <see cref="Localized{T}"/>, so that
/// <c>Title</c> becomes <c>title_i18n</c> (design M0 section 3.1). One place decides column names,
/// on top of the snake case convention.
/// </summary>
public sealed class LocalizedColumnConvention : IModelFinalizingConvention
{
    private const string Suffix = "_i18n";

    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var property in modelBuilder.Metadata.GetEntityTypes().SelectMany(entity => entity.GetProperties()))
        {
            if (!IsLocalized(property.ClrType))
            {
                continue;
            }

            var name = property.GetColumnName();
            if (name is not null && !name.EndsWith(Suffix, StringComparison.Ordinal))
            {
                property.Builder.HasColumnName(name + Suffix);
            }
        }
    }

    private static bool IsLocalized(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Localized<>);
}
