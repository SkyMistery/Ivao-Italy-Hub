using System.Security.Cryptography;
using System.Text;

namespace IvaoHub.Core.Content;

/// <summary>
/// The address a file is read at. One place builds it, because it goes into a list row, into a
/// picker, into the properties of a block and into an already published page — and an address
/// assembled in four places is an address that changes in three.
/// <para>The shape is <c>/media/{id}/{fingerprint}/{name}</c>: the identifier decides which row is
/// served, the fingerprint says which file of that row, and the name is there so that a browser
/// saving the file calls it what it was called.</para>
/// <para>⚠️ The fingerprint exists since G20, when a file could be replaced on the same row (note
/// 2026-09-13-contenuti-centralizzati §3.4): a public file is cached as <c>immutable</c> for a year,
/// so the address has to change with the bytes. An address that carries the current fingerprint is
/// served that way; one without a fingerprint — what a page body builds from the identifier alone —
/// or with an old one is served asking the cache to check again, so it is never stale and never
/// broken.</para>
/// </summary>
public static class MediaUrl
{
    /// <summary>The route of the address this class builds. Excluded from the SPA fallback.</summary>
    public const string Pattern = "/media/{id:long}/{fingerprint}/{name}";

    /// <summary>The address without a fingerprint, which a body builds from the identifier alone.</summary>
    public const string UnversionedPattern = "/media/{id:long}/{name}";

    /// <summary>The prefix, for whoever has to keep the single page application off it.</summary>
    public const string Prefix = "/media";

    /// <summary>How many hexadecimal characters of the hash go into the address.</summary>
    public const int FingerprintLength = 12;

    public static string For(MediaAsset media)
    {
        ArgumentNullException.ThrowIfNull(media);
        return $"{Prefix}/{media.Id}/{Fingerprint(media)}/{Slug(media.FileName)}";
    }

    /// <summary>
    /// Which file of the row this is: the start of the hash of its bytes, or — for the rows uploaded
    /// before the hash was kept — of its name on disk, which changes whenever the file does.
    /// </summary>
    public static string Fingerprint(MediaAsset media)
    {
        ArgumentNullException.ThrowIfNull(media);

        var hash = media.Sha256
            ?? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(media.StoredName)));

        return hash[..Math.Min(hash.Length, FingerprintLength)];
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
