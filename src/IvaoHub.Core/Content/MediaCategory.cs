namespace IvaoHub.Core.Content;

/// <summary>
/// What a file of the library is for. A closed set and not free text (issue #359, note
/// 2026-10-28-le-categorie-dei-media): staff who did not build the site wrote "logo", "Loghi" and
/// "logos" for the same thing, and a picker that shows only the logos needs one word for them.
/// <para>Stored as text, like every enum of the hub. There is no <c>Video</c>: the library takes no
/// video file, and a video is an address on a host the hub knows how to frame (Carmine, 28 October
/// 2026).</para>
/// </summary>
public enum MediaCategory
{
    Image,
    Icon,
    Logo,
    Banner,
    Document,
}

/// <summary>Which category a file gets when nobody has said yet: what its bytes are.</summary>
public static class MediaCategories
{
    /// <summary>A PDF is a document; anything else this library takes is a picture.</summary>
    public static MediaCategory For(string contentType) =>
        contentType.StartsWith("image/", StringComparison.Ordinal) ? MediaCategory.Image : MediaCategory.Document;
}
