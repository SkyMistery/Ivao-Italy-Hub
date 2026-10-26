using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IvaoHub.Core.Ivao;

/// <summary>
/// The narrowed picture of the network kept on disk for the next process (issue #397, note
/// 2026-10-26-la-foto-della-rete-oltre-il-processo). On the host a process lives about thirty seconds, so the minute of
/// the cache in memory almost never outlives it: 183 processes out of 400 downloaded the several megabytes again. A
/// process born within the minute reads this file instead.
/// <para>What is written is only what <see cref="IvaoWhazzup.Read"/> kept — callsigns, stations, frequencies and the two
/// ends of each flight plan, nobody's name or number — with the moment this installation took it. It lives under the
/// installation's <c>cache/</c> folder, never under <c>wwwroot/</c>.</para>
/// <para><b>Its read must cost less than the download it saves</b>: a few tens of kilobytes of tab separated lines, read
/// without reflection, and a file larger than <see cref="MaxBytes"/> is not read at all. Whatever goes wrong — no file,
/// an old one, half a file, a shape this version does not know — is "no file", and the caller downloads as before.
/// The write goes to a name of its own and is renamed over the file, so a reader never sees half of it.</para>
/// </summary>
public sealed class IvaoNetworkPictureStore
{
    /// <summary>The file's name in its folder.</summary>
    public const string FileName = "network-picture.txt";

    /// <summary>A narrowed evening is tens of kilobytes; a file a hundred times larger is not one of ours.</summary>
    public const long MaxBytes = 4 * 1024 * 1024;

    /// <summary>The first line, with the version of the shape: another shape is no file.</summary>
    private const string Header = "ivao-network-picture 1";

    private readonly string? _file;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    public IvaoNetworkPictureStore(string? folder, TimeProvider time, ILogger<IvaoNetworkPictureStore> logger)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        _file = string.IsNullOrWhiteSpace(folder) ? null : Path.Combine(folder, FileName);
        _time = time;
        _logger = logger;
    }

    /// <summary>No file at all: what a client built by hand in a test uses, so that tests never share a picture.</summary>
    public static IvaoNetworkPictureStore None { get; } =
        new(null, TimeProvider.System, NullLogger<IvaoNetworkPictureStore>.Instance);

    /// <summary>
    /// The picture another process took less than <paramref name="freshness"/> ago, with what is left of its minute; null
    /// when there is none worth reading.
    /// </summary>
    public (IvaoNetworkPicture Picture, TimeSpan Left)? Read(TimeSpan freshness)
    {
        if (_file is null)
        {
            return null;
        }

        try
        {
            var info = new FileInfo(_file);
            if (!info.Exists || info.Length > MaxBytes)
            {
                return null;
            }

            using var stream = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return Parse(reader, freshness);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(failure, "The kept picture of the network could not be read; it is downloaded.");
            return null;
        }
    }

    /// <summary>Keeps <paramref name="picture"/> for the next process, taken now. Never throws: a failed write is a log line.</summary>
    public void Write(IvaoNetworkPicture picture)
    {
        ArgumentNullException.ThrowIfNull(picture);

        if (_file is null || !IsPlain(picture))
        {
            return;
        }

        var temporary = $"{_file}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            using (var writer = new StreamWriter(temporary, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                Format(writer, picture, _time.GetUtcNow().UtcDateTime);
            }

            File.Move(temporary, _file, overwrite: true);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(failure, "The picture of the network could not be kept in {File}.", _file);
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // Nothing more to do: a stray temporary file is harmless, and the folder may be deleted at any time.
            }
        }
    }

    private static void Format(TextWriter writer, IvaoNetworkPicture picture, DateTime taken)
    {
        writer.Write(Header);
        writer.Write('\n');
        writer.Write(string.Create(CultureInfo.InvariantCulture, $"taken\t{taken.Ticks}\n"));
        writer.Write(picture.UpdatedAt is { } updated
            ? string.Create(CultureInfo.InvariantCulture, $"updated\t{updated.Ticks}\n")
            : "updated\t-\n");
        writer.Write(string.Create(CultureInfo.InvariantCulture, $"network\t{picture.NetworkAtc}\t{picture.NetworkPilots}\n"));

        foreach (var controller in picture.Controllers)
        {
            writer.Write($"C\t{controller.Callsign}\t{controller.Station}\t{controller.Frequency ?? "-"}\n");
        }

        foreach (var flight in picture.Flights)
        {
            writer.Write($"F\t{flight.Departure ?? "-"}\t{flight.Arrival ?? "-"}\n");
        }
    }

    private (IvaoNetworkPicture Picture, TimeSpan Left)? Parse(TextReader reader, TimeSpan freshness)
    {
        if (reader.ReadLine() != Header
            || Field(reader.ReadLine(), "taken") is not { } takenText
            || !long.TryParse(takenText, NumberStyles.None, CultureInfo.InvariantCulture, out var takenTicks)
            || takenTicks < DateTime.MinValue.Ticks || takenTicks > DateTime.MaxValue.Ticks)
        {
            return null;
        }

        // Older than its minute, or taken "later" than now (a clock moved back): no file.
        var age = _time.GetUtcNow().UtcDateTime - new DateTime(takenTicks, DateTimeKind.Utc);
        if (age < TimeSpan.Zero || age >= freshness)
        {
            return null;
        }

        if (Field(reader.ReadLine(), "updated") is not { } updatedText
            || reader.ReadLine()?.Split('\t') is not ["network", var atcText, var pilotsText]
            || !int.TryParse(atcText, NumberStyles.None, CultureInfo.InvariantCulture, out var networkAtc)
            || !int.TryParse(pilotsText, NumberStyles.None, CultureInfo.InvariantCulture, out var networkPilots))
        {
            return null;
        }

        // A failure is kept too, as the cache in memory keeps it: one call a minute to a network that is down.
        if (updatedText == "-")
        {
            return (IvaoNetworkPicture.Unknown, freshness - age);
        }

        if (!long.TryParse(updatedText, NumberStyles.None, CultureInfo.InvariantCulture, out var updatedTicks)
            || updatedTicks < DateTime.MinValue.Ticks || updatedTicks > DateTime.MaxValue.Ticks)
        {
            return null;
        }

        var controllers = new List<IvaoNetworkPosition>();
        var flights = new List<IvaoFlightEnds>();
        while (reader.ReadLine() is { } line)
        {
            switch (line.Split('\t'))
            {
                case ["C", { Length: > 0 } callsign, { Length: > 0 } station, var frequency]:
                    controllers.Add(new IvaoNetworkPosition(callsign, station, frequency == "-" ? null : frequency));
                    break;
                case ["F", var departure, var arrival]:
                    flights.Add(new IvaoFlightEnds(departure == "-" ? null : departure, arrival == "-" ? null : arrival));
                    break;
                default:
                    return null;
            }
        }

        var picture = new IvaoNetworkPicture(
            new DateTime(updatedTicks, DateTimeKind.Utc),
            networkAtc,
            networkPilots,
            controllers,
            flights);

        return (picture, freshness - age);
    }

    private static string? Field(string? line, string name) =>
        line?.Split('\t') is [var key, var value] && key == name ? value : null;

    /// <summary>
    /// Whether every text of the reading can be written as a field: no tab, no line break, and not the dash that stands
    /// for "nothing". The network never sends one; if it ever did, the picture is not kept rather than kept wrong.
    /// </summary>
    private static bool IsPlain(IvaoNetworkPicture picture)
    {
        return picture.Controllers.All(controller =>
                Plain(controller.Callsign) && Plain(controller.Station) && (controller.Frequency is null || Plain(controller.Frequency)))
            && picture.Flights.All(flight =>
                (flight.Departure is null || Plain(flight.Departure)) && (flight.Arrival is null || Plain(flight.Arrival)));

        static bool Plain(string text) =>
            text.Length > 0 && text != "-" && text.IndexOfAny(['\t', '\n', '\r']) < 0;
    }
}
