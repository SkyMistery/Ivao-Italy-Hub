namespace IvaoHub.Core.Content;

/// <summary>
/// The address a file is read at. One place builds it, because it goes into a list row, into a
/// picker, into the properties of a block and into an already published page — and an address
/// assembled in four places is an address that changes in three.
/// <para>The shape is <c>/media/{id}/{name}</c>: the identifier is what decides which row is
/// served, and the name is there so that a browser saving the file calls it what it was called.
/// Changing the name therefore changes the address without changing what it serves.</para>
/// </summary>
public static class MediaUrl
{
    /// <summary>The route the files are served from. Excluded from the SPA fallback.</summary>
    public const string Pattern = "/media/{id:long}/{name}";

    /// <summary>The prefix, for whoever has to keep the single page application off it.</summary>
    public const string Prefix = "/media";

    public static string For(MediaAsset media)
    {
        ArgumentNullException.ThrowIfNull(media);
        return $"{Prefix}/{media.Id}/{Slug(media.FileName)}";
    }

    /// <summary>
    /// The uploaded name made safe to put in a path: letters, digits, dots and dashes, and nothing
    /// else. It is decoration — the identifier is what is looked up — so a name that reduces to
    /// nothing simply becomes <c>file</c> rather than an error.
    /// </summary>
    public static string Slug(string fileName)
    {
        var kept = new string([.. (fileName ?? string.Empty)
            .Select(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'
                ? char.ToLowerInvariant(character)
                : '-')]);

        var trimmed = kept.Trim('-', '.', '_');
        return trimmed.Length == 0 ? "file" : trimmed[..Math.Min(trimmed.Length, 80)];
    }
}
