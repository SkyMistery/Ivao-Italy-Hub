using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Privacy;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The test module's half of erasing a person (T20b, note 2026-09-25-la-cancellazione-dei-dati-di-una-persona): the rows
/// about them go — except one whose title says to keep it, which stays with the VID the way a ban still in force does
/// (note 2026-09-25-le-righe-che-restano-con-il-vid). What it does not write is the point: the rows they only created are
/// left to the core, which gives them the pseudonym, and so is the audit of what this deletes.
/// </summary>
public sealed class SampleEraser(SampleDbContext database) : IPersonalDataEraser
{
    public const string ItemsKey = "sample:erasure.items";

    public const string KeptKey = "sample:erasure.kept";

    /// <summary>The title of a row about the person that the module keeps as it is.</summary>
    public const string KeptPrefix = "erasure-keep";

    public string ModuleKey => SampleModule.ModuleKey;

    public async Task<IReadOnlyList<ErasureLine>> PreviewAsync(int vid, CancellationToken cancellationToken = default)
    {
        var titles = await CrudSource.BackOffice<SampleItem>(database)
            .Where(row => row.StakeholderVid == vid)
            .Select(row => row.Title)
            .ToListAsync(cancellationToken);

        var kept = titles.Count(title => title.StartsWith(KeptPrefix, StringComparison.Ordinal));
        return [new(ItemsKey, titles.Count - kept, ErasureOutcome.Deleted), new(KeptKey, kept, ErasureOutcome.Kept)];
    }

    public async Task<IReadOnlyList<ErasureLine>> EraseAsync(ErasureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rows = await CrudSource.BackOffice<SampleItem>(database).AsTracking()
            .Where(row => row.StakeholderVid == request.Vid)
            .ToListAsync(cancellationToken);

        var kept = rows.Where(row => row.Title.StartsWith(KeptPrefix, StringComparison.Ordinal)).ToList();
        foreach (var row in kept)
        {
            request.Keep(row);
        }

        database.Items.RemoveRange(rows.Except(kept));
        await database.SaveChangesAsync(cancellationToken);
        return [new(ItemsKey, rows.Count - kept.Count, ErasureOutcome.Deleted), new(KeptKey, kept.Count, ErasureOutcome.Kept)];
    }
}
