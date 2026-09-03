using System.Text.Json;
using System.Text.Json.Serialization;

namespace IvaoHub.Core.Localization;

/// <summary>
/// A localized field crosses the API as the plain object <c>{ "en": "…", "it": "…" }</c>: the
/// single page application always receives every language and resolves the one it needs, so there
/// is no "give me this page in Italian" endpoint anywhere (design M0 section 3.1).
/// <para>Registered once in the global JSON options; no DTO ever declares its own converter.</para>
/// </summary>
public sealed class LocalizedJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Localized<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        var valueType = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(typeof(LocalizedJsonConverter<>).MakeGenericType(valueType))!;
    }
}

/// <summary>The converter for one value type; built by the factory, never registered by hand.</summary>
public sealed class LocalizedJsonConverter<T> : JsonConverter<Localized<T>>
{
    /// <summary>A missing field is an empty value, not null: no caller has to guard for both.</summary>
    public override bool HandleNull => true;

    public override Localized<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return Localized<T>.Empty;
        }

        var values = JsonSerializer.Deserialize<Dictionary<string, T>>(ref reader, options);
        return values is null ? Localized<T>.Empty : new Localized<T>(values);
    }

    public override void Write(Utf8JsonWriter writer, Localized<T> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        foreach (var (locale, item) in value)
        {
            writer.WritePropertyName(locale);
            JsonSerializer.Serialize(writer, item, options);
        }

        writer.WriteEndObject();
    }
}
