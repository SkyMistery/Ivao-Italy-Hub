using System.Text.Json.Nodes;
using IvaoHub.Core.Localization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace IvaoHub.Web.OpenApi;

/// <summary>
/// Describes every <see cref="Localized{T}"/> in the document as the object it is on the wire —
/// <c>{ "en": "…", "it": "…" }</c> — and marks it with <c>x-localized: true</c>. The form generator
/// of the SPA reads that extension to draw a language tabbed field instead of a plain one, so "this
/// field is translated" is said once, in the contract, and never again in a hand written client
/// type (design M0 sections 3.1 and 7.4).
/// <para>The shape has to be written here because the type carries a JSON converter of its own,
/// and a type with a converter is opaque to schema generation: left alone it would reach TypeScript
/// as <c>unknown</c>.</para>
/// </summary>
internal sealed class LocalizedSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>The extension the form generator looks for.</summary>
    public const string ExtensionName = "x-localized";

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        var type = context.JsonTypeInfo.Type;
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Localized<>))
        {
            return Task.CompletedTask;
        }

        schema.Type = JsonSchemaType.Object;

        // A language maps to a string for a text field; a Localized<JsonNode> holds whatever the
        // block put there, and the contract says no more than that.
        schema.AdditionalProperties = type.GetGenericArguments()[0] == typeof(string)
            ? new OpenApiSchema { Type = JsonSchemaType.String }
            : new OpenApiSchema();

        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
        schema.Extensions[ExtensionName] = new JsonNodeExtension(JsonValue.Create(true));

        return Task.CompletedTask;
    }
}
