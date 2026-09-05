using System.Buffers.Binary;

namespace IvaoHub.Core.Content;

/// <summary>The pixel size of an image, when the header says it.</summary>
public readonly record struct ImageSize(int Width, int Height);

/// <summary>
/// Reads the width and the height out of the first bytes of an image, for PNG, JPEG and WebP and
/// for nothing else (implementation plan M1, G1, decided 5 September 2026).
/// <para>Why a parser and not a library: <c>ImageSharp</c> has a licence to check before it goes
/// into this repository, and <c>SkiaSharp</c> means native assets inside a self contained
/// linux-x64 package — a deployment problem in exchange for two numbers. Sixty lines that read a
/// header cost neither.</para>
/// <para>⚠️ The three formats <b>are</b> the perimeter. A format that needs more than this is a
/// decision with a note, not a quiet extension of this file.</para>
/// </summary>
public static class ImageHeader
{
    /// <summary>
    /// How much of a file has to be read for the parser to have a chance. A JPEG carries its size
    /// in a segment that comes after the metadata, and a camera puts a whole thumbnail in there.
    /// </summary>
    public const int ProbeBytes = 128 * 1024;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// The size of the image, or null when these bytes are not one of the three formats or do not
    /// carry it. Null is an answer and not a failure: the row simply has no width and no height.
    /// </summary>
    public static ImageSize? Read(ReadOnlySpan<byte> bytes)
    {
        if (TryPng(bytes, out var png))
        {
            return png;
        }

        if (TryJpeg(bytes, out var jpeg))
        {
            return jpeg;
        }

        return TryWebp(bytes, out var webp) ? webp : null;
    }

    /// <summary>PNG: the IHDR chunk is the first one, and its first two fields are the size.</summary>
    private static bool TryPng(ReadOnlySpan<byte> bytes, out ImageSize size)
    {
        size = default;

        if (bytes.Length < 24 || !bytes[..8].SequenceEqual(PngSignature) || !Is(bytes[12..16], "IHDR"))
        {
            return false;
        }

        size = new ImageSize(
            BinaryPrimitives.ReadInt32BigEndian(bytes[16..20]),
            BinaryPrimitives.ReadInt32BigEndian(bytes[20..24]));

        return size.Width > 0 && size.Height > 0;
    }

    /// <summary>
    /// JPEG: a walk over the markers until a start of frame, which is the only segment that
    /// carries the size. Everything before it — the metadata, a thumbnail — is skipped by length.
    /// </summary>
    private static bool TryJpeg(ReadOnlySpan<byte> bytes, out ImageSize size)
    {
        size = default;

        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
        {
            return false;
        }

        var offset = 2;
        while (offset + 4 <= bytes.Length)
        {
            // Markers may be padded with any number of 0xFF bytes; the payload never is.
            if (bytes[offset] != 0xFF)
            {
                return false;
            }

            var marker = bytes[offset + 1];
            offset += 2;

            if (marker is 0xFF or 0x01 or (>= 0xD0 and <= 0xD9))
            {
                continue;
            }

            if (offset + 2 > bytes.Length)
            {
                return false;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..(offset + 2)]);
            if (length < 2)
            {
                return false;
            }

            // C4, C8 and CC are Huffman tables, arithmetic conditioning and definitions, not frames.
            var isStartOfFrame = marker is (>= 0xC0 and <= 0xCF) and not (0xC4 or 0xC8 or 0xCC);
            if (isStartOfFrame)
            {
                if (offset + 7 > bytes.Length)
                {
                    return false;
                }

                size = new ImageSize(
                    BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 5)..(offset + 7)]),
                    BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 3)..(offset + 5)]));

                return size.Width > 0 && size.Height > 0;
            }

            offset += length;
        }

        return false;
    }

    /// <summary>
    /// WebP: a RIFF container whose first chunk says which of the three encodings it is, and each
    /// of the three writes the size in its own way.
    /// </summary>
    private static bool TryWebp(ReadOnlySpan<byte> bytes, out ImageSize size)
    {
        size = default;

        if (bytes.Length < 30 || !Is(bytes[..4], "RIFF") || !Is(bytes[8..12], "WEBP"))
        {
            return false;
        }

        // Lossy: three bytes of frame tag, the three byte sync code, then two 14 bit fields.
        if (Is(bytes[12..16], "VP8 "))
        {
            if (bytes[23] != 0x9D || bytes[24] != 0x01 || bytes[25] != 0x2A)
            {
                return false;
            }

            size = new ImageSize(
                BinaryPrimitives.ReadUInt16LittleEndian(bytes[26..28]) & 0x3FFF,
                BinaryPrimitives.ReadUInt16LittleEndian(bytes[28..30]) & 0x3FFF);

            return size.Width > 0 && size.Height > 0;
        }

        // Lossless: one signature byte, then two 14 bit fields packed into 32 bits, each less one.
        if (Is(bytes[12..16], "VP8L"))
        {
            if (bytes[20] != 0x2F)
            {
                return false;
            }

            var packed = BinaryPrimitives.ReadUInt32LittleEndian(bytes[21..25]);
            size = new ImageSize(
                (int)((packed & 0x3FFF) + 1),
                (int)(((packed >> 14) & 0x3FFF) + 1));

            return true;
        }

        // Extended: the canvas size, two 24 bit fields each less one, after four bytes of flags.
        if (Is(bytes[12..16], "VP8X"))
        {
            size = new ImageSize(
                Read24(bytes[24..27]) + 1,
                Read24(bytes[27..30]) + 1);

            return true;
        }

        return false;
    }

    private static int Read24(ReadOnlySpan<byte> bytes) => bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);

    private static bool Is(ReadOnlySpan<byte> bytes, string ascii)
    {
        for (var index = 0; index < ascii.Length; index++)
        {
            if (bytes[index] != (byte)ascii[index])
            {
                return false;
            }
        }

        return true;
    }
}
