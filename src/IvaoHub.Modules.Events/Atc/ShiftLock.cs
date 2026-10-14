using System.Data;
using IvaoHub.Modules.Events.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IvaoHub.Modules.Events.Atc;

/// <summary>
/// The lock a correction of the roster holds (the review of #262, point 1): the event's row <c>FOR UPDATE</c>, in a transaction that reads
/// what is committed, taken by the save's checks (<c>BeforeSave</c>) and committed once the save is done (<c>AfterSave</c>) — the CRUD
/// engine's own save writes the shift and its audit inside it. A refused write commits nothing: the transaction goes with the request's
/// context, rolled back. One per request.
/// </summary>
public sealed class ShiftLock
{
    private IDbContextTransaction? _transaction;

    /// <summary>Begins the transaction, unless the request runs in one already, and locks the event's row in it.</summary>
    public async Task TakeAsync(EventsDbContext database, long eventId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (database.Database.CurrentTransaction is null)
        {
            _transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        }

        await database.Database
            .SqlQuery<long>($"SELECT id AS `Value` FROM evt_events WHERE id = {eventId} FOR UPDATE")
            .ToListAsync(cancellationToken);
    }

    /// <summary>Commits the transaction this lock began, if it began one.</summary>
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}
