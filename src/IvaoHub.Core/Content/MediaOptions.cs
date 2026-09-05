using System.ComponentModel.DataAnnotations;

namespace IvaoHub.Core.Content;

/// <summary>
/// What an installation is willing to accept into its media library, and where the files go.
/// Bound from the <c>Media</c> section and validated at start up like every other option object:
/// a limit that only exists in a comparison inside an endpoint is a limit nobody can change
/// without a release (design M1 section 2).
/// </summary>
public sealed record MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Largest upload accepted, in bytes. Eight megabytes covers a photograph.</summary>
    [Range(1, 512 * 1024 * 1024)]
    public long MaxBytes { get; init; } = 8 * 1024 * 1024;

    /// <summary>
    /// The types allowed in, as content types. They are matched against what the <b>bytes</b> say
    /// and never against what the browser claimed, so a name or a header cannot smuggle a type in;
    /// a type this hub cannot recognise from its first bytes is therefore not one it accepts.
    /// </summary>
    public string[] AllowedContentTypes { get; init; } =
    [
        "image/png",
        "image/jpeg",
        "image/webp",
        "image/gif",
        "application/pdf",
    ];

    /// <summary>
    /// Where the files live. Empty means <c>HubPaths.Media</c>, next to the other folders that are
    /// not code; a deployment that keeps uploads on a disk of their own sets an absolute path here.
    /// </summary>
    public string Directory { get; init; } = string.Empty;
}
