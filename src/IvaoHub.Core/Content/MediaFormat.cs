namespace IvaoHub.Core.Content;

/// <summary>A file type this hub recognises, as the content type it is served with.</summary>
/// <param name="ContentType">What the response says it is.</param>
/// <param name="Extension">The suffix of the name on disk, dot included.</param>
public sealed record MediaFormat(string ContentType, string Extension)
{
    /// <summary>Whether the pixel size of this format can be read from its header.</summary>
    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);
}

/// <summary>
/// What a file actually is, read from its first bytes. The type the browser declared is never
/// believed: a multipart part carries whatever content type its sender wrote, and an upload that
/// says <c>image/png</c> while holding a page of HTML is the oldest trick there is.
/// <para>Recognising a type and allowing it are two different questions: this answers the first,
/// <see cref="MediaOptions.AllowedContentTypes"/> answers the second.</para>
/// </summary>
public static class MediaFormats
{
    /// <summary>Enough bytes for every signature below.</summary>
    public const int SignatureBytes = 16;

    private static readonly MediaFormat Png = new("image/png", ".png");
    private static readonly MediaFormat Jpeg = new("image/jpeg", ".jpg");
    private static readonly MediaFormat Webp = new("image/webp", ".webp");
    private static readonly MediaFormat Gif = new("image/gif", ".gif");
    private static readonly MediaFormat Pdf = new("application/pdf", ".pdf");

    /// <summary>The format of these bytes, or null when it is none this hub knows.</summary>
    public static MediaFormat? Detect(ReadOnlySpan<byte> bytes)
    {
        if (Starts(bytes, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Png;
        }

        if (Starts(bytes, [0xFF, 0xD8, 0xFF]))
        {
            return Jpeg;
        }

        if (bytes.Length >= 12 && Ascii(bytes, 0, "RIFF") && Ascii(bytes, 8, "WEBP"))
        {
            return Webp;
        }

        if (bytes.Length >= 6 && Ascii(bytes, 0, "GIF8"))
        {
            return Gif;
        }

        return bytes.Length >= 5 && Ascii(bytes, 0, "%PDF-") ? Pdf : null;
    }

    private static bool Starts(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> signature) =>
        bytes.Length >= signature.Length && bytes[..signature.Length].SequenceEqual(signature);

    private static bool Ascii(ReadOnlySpan<byte> bytes, int offset, string expected)
    {
        for (var index = 0; index < expected.Length; index++)
        {
            if (bytes[offset + index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }
}
