using System.Text.Json.Nodes;

namespace IvaoHub.Core.Content;

/// <summary>
/// Copying the body of a template into a row of its own.
/// <para>It lives here rather than inside the endpoint that first needed it because there are now
/// two callers — "new from a template", and the seed that gives every department its dashboard —
/// and a second copy of these fifteen lines is a second answer to "what does a page inherit from a
/// template", which is precisely the question design M1 section 9.1 is built on.</para>
/// </summary>
public static class TemplateCopy
{
    /// <summary>
    /// A deep copy is only a copy if nothing in it still answers to the old name: every section and
    /// block gets a fresh identifier, any capture from the template is dropped, and the keys only a
    /// template may carry are left behind — a page holding them would be able to lift its own
    /// restrictions, which is exactly what the envelope validator refuses (design M0 section 5.2).
    /// </summary>
    public static void Reidentify(BlockDocumentWalker walker, JsonNode body)
    {
        ArgumentNullException.ThrowIfNull(walker);
        ArgumentNullException.ThrowIfNull(body);

        foreach (var section in walker.EnumerateSections(body))
        {
            section.Node["id"] = NewId("s");
            section.Node.Remove("required");
            section.Node.Remove("locked");
            section.Node.Remove("allowedBlocks");
        }

        foreach (var block in walker.EnumerateBlocks(body))
        {
            block.Node["id"] = NewId("b");
            block.Node["frozen"] = null;
        }
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..10];
}
