using System.IO.Compression;
using System.Text.Json;
using IvaoHub.Core.Ivao;

namespace IvaoHub.Modules.FlightOps.Pireps;

/// <summary>
/// A track as <c>fo_pirep_tracks</c> keeps it (note 2026-09-23-la-validazione §2.2): the points the tracker gave, as JSON
/// compressed with gzip. Nothing is dropped — the checks of T18 read what the map does not — and a flight of three hours
/// is about 22 KB instead of 280.
/// </summary>
public static class TrackCodec
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static PirepTrack Encode(IReadOnlyList<IvaoTrackPointDto> points, DateTime at)
    {
        ArgumentNullException.ThrowIfNull(points);

        using var buffer = new MemoryStream();
        using (var zip = new GZipStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            JsonSerializer.Serialize(zip, points, Json);
        }

        return new PirepTrack { PointsGzip = buffer.ToArray(), PointCount = points.Count, StoredAt = at };
    }

    public static IReadOnlyList<IvaoTrackPointDto> Decode(PirepTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        using var zip = new GZipStream(new MemoryStream(track.PointsGzip), CompressionMode.Decompress);
        return JsonSerializer.Deserialize<List<IvaoTrackPointDto>>(zip, Json) ?? [];
    }
}
