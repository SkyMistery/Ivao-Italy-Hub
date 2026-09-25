using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Privacy;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The test module's half of erasing a person (T20b, note 2026-09-25-la-cancellazione-dei-dati-di-una-persona): the rows
/// about them go. What it does not write is the point — the rows they only created are left to the core, which gives them
/// the pseudonym, and so is the audit of what this deletes.
/// </summary>
public sealed class SampleEraser(SampleDbContext database) : IPersonalDataEraser
{
    public const string ItemsKey = "sample:erasure.items";

    public string ModuleKey => SampleModule.ModuleKey;

    public async Task<IReadOnlyList<ErasureLine>> PreviewAsync(int vid, CancellationToken cancellationToken = default) =>
        [new(ItemsKey, await CrudSource.BackOffice<SampleItem>(database).CountAsync(row => row.StakeholderVid == vid, cancellationToken), ErasureOutcome.Deleted)];

    public async Task<IReadOnlyList<ErasureLine>> EraseAsync(ErasureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rows = await CrudSource.BackOffice<SampleItem>(database).AsTracking()
            .Where(row => row.StakeholderVid == request.Vid)
            .ToListAsync(cancellationToken);

        database.Items.RemoveRange(rows);
        await database.SaveChangesAsync(cancellationToken);
        return [new(ItemsKey, rows.Count, ErasureOutcome.Deleted)];
    }
}
