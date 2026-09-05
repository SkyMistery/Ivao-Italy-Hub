using System.Buffers.Binary;
using System.Text;
using IvaoHub.Core.Content;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The sixty lines that stand in for an imaging library (implementation plan M1, G1). They are worth
/// testing byte by byte because nothing else will notice when they are wrong: a width read a
/// little off simply becomes a slightly wrong number in a column nobody looks at twice.
/// <para>The headers are built here rather than checked in as files, so that what each test is
/// about is visible in the test.</para>
/// </summary>
public sealed class ImageHeaderTests
{
    [Fact]
    public void ReadsAPngFromItsFirstChunk()
    {
        Assert.Equal(new ImageSize(1280, 720), ImageHeader.Read(Png(1280, 720)));
    }

    [Fact]
    public void ReadsAJpegPastItsMetadata()
    {
        // The size of a JPEG is in a segment that comes after the metadata, and a camera puts a
        // whole thumbnail in there: a parser that only looked at the first segment would find
        // nothing on most real photographs.
        Assert.Equal(new ImageSize(4032, 3024), ImageHeader.Read(Jpeg(4032, 3024, metadataBytes: 6000)));
    }

    [Fact]
    public void ReadsTheThreeShapesOfWebp()
    {
        Assert.Equal(new ImageSize(300, 200), ImageHeader.Read(WebpLossy(300, 200)));
        Assert.Equal(new ImageSize(300, 200), ImageHeader.Read(WebpLossless(300, 200)));
        Assert.Equal(new ImageSize(300, 200), ImageHeader.Read(WebpExtended(300, 200)));
    }

    [Fact]
    public void AnswersNothingRatherThanGuessing()
    {
        // A format outside the perimeter, a file cut short, and nothing at all. None of the three is
        // a failure: a row simply has no width and no height, and a media is still usable.
        Assert.Null(ImageHeader.Read(Encoding.ASCII.GetBytes("%PDF-1.7\n%âãÏÓ")));
        Assert.Null(ImageHeader.Read(Png(1280, 720).AsSpan(0, 20)));
        Assert.Null(ImageHeader.Read([]));
    }

    [Fact]
    public void RecognisesOnlyWhatItCanServe()
    {
        Assert.Equal("image/png", MediaFormats.Detect(Png(1, 1))?.ContentType);
        Assert.Equal("image/jpeg", MediaFormats.Detect(Jpeg(1, 1, metadataBytes: 0))?.ContentType);
        Assert.Equal("image/webp", MediaFormats.Detect(WebpLossy(1, 1))?.ContentType);
        Assert.Equal("application/pdf", MediaFormats.Detect(Encoding.ASCII.GetBytes("%PDF-1.7"))?.ContentType);

        // The one that matters: a file whose bytes are HTML is not a picture, whatever the browser
        // said it was uploading. The upload refuses what it cannot recognise.
        Assert.Null(MediaFormats.Detect(Encoding.ASCII.GetBytes("<!doctype html><script>")));
    }

    // ---- headers, written out so that a test says what it is about --------------------------

    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[33];
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes);

        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 13);
        Encoding.ASCII.GetBytes("IHDR").CopyTo(bytes, 12);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);

        return bytes;
    }

    private static byte[] Jpeg(int width, int height, int metadataBytes)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };

        if (metadataBytes > 0)
        {
            // An APP1 segment of the given size, standing in for the Exif block of a photograph.
            bytes.AddRange([0xFF, 0xE1]);
            bytes.AddRange(BigEndian((ushort)(metadataBytes + 2)));
            bytes.AddRange(new byte[metadataBytes]);
        }

        // A baseline start of frame: length, precision, height, width, components.
        bytes.AddRange([0xFF, 0xC0]);
        bytes.AddRange(BigEndian(11));
        bytes.Add(8);
        bytes.AddRange(BigEndian((ushort)height));
        bytes.AddRange(BigEndian((ushort)width));
        bytes.AddRange([1, 1, 0x11, 0]);

        return [.. bytes];
    }

    private static byte[] WebpLossy(int width, int height)
    {
        var bytes = Riff("VP8 ");
        bytes[23] = 0x9D;
        bytes[24] = 0x01;
        bytes[25] = 0x2A;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(28), (ushort)height);
        return bytes;
    }

    private static byte[] WebpLossless(int width, int height)
    {
        var bytes = Riff("VP8L");
        bytes[20] = 0x2F;
        var packed = (uint)((width - 1) | ((height - 1) << 14));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(21), packed);
        return bytes;
    }

    private static byte[] WebpExtended(int width, int height)
    {
        var bytes = Riff("VP8X");
        Write24(bytes.AsSpan(24), width - 1);
        Write24(bytes.AsSpan(27), height - 1);
        return bytes;
    }

    private static byte[] Riff(string chunk)
    {
        var bytes = new byte[32];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("WEBP").CopyTo(bytes, 8);
        Encoding.ASCII.GetBytes(chunk).CopyTo(bytes, 12);
        return bytes;
    }

    private static void Write24(Span<byte> target, int value)
    {
        target[0] = (byte)(value & 0xFF);
        target[1] = (byte)((value >> 8) & 0xFF);
        target[2] = (byte)((value >> 16) & 0xFF);
    }

    private static byte[] BigEndian(ushort value) => [(byte)(value >> 8), (byte)(value & 0xFF)];
}
