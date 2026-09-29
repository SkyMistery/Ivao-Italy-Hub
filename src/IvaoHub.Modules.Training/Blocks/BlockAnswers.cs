using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using IvaoHub.Core.Localization;

namespace IvaoHub.Modules.Training.Blocks;

/// <summary>
/// How the blocks of the training answer (design M3 §4.3): in the shape the HTTP answers have, so the browser reads a block and an
/// endpoint with one type — translated values whole, the states and the ladders by name. A personal block says whether its reader is
/// signed in: a visitor gets <c>signedIn: false</c> and nothing else, as the tours' own block does.
/// </summary>
internal static class BlockAnswers
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LocalizedJsonConverterFactory(), new JsonStringEnumConverter() },
    };

    /// <summary>What a personal block tells a visitor: nothing is theirs.</summary>
    public static JsonObject SignedOut() => new() { ["signedIn"] = false };

    /// <summary>An answer for a signed in reader, as its HTTP shape, with <c>signedIn: true</c> beside it.</summary>
    public static JsonObject SignedIn<T>(T answer)
    {
        var node = JsonSerializer.SerializeToNode(answer, Json)!.AsObject();
        node["signedIn"] = true;
        return node;
    }

    /// <summary>A value as its HTTP shape.</summary>
    public static JsonNode? Of<T>(T value) => JsonSerializer.SerializeToNode(value, Json);
}
