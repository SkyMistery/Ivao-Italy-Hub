using MySqlConnector;

namespace IvaoHub.Modules.Events.Data;

/// <summary>
/// What the database answered when a save of the events failed on another write of the same rows — the only failures a verb of
/// the module answers by itself; every other one surfaces as itself. Whether another write took the key — with or without the
/// deadlock two inserts of one key meet in — is the core's question, <see cref="IvaoHub.Core.Data.WriteConflicts"/>, and never
/// asked here (#499): <c>AnotherWriteWon</c> where a deadlock is the same news, <c>DuplicateKey</c> where a pilot is told «taken» and
/// a deadlock is «try again» (the review of #233, point 3). What stays here are the module's own questions, on its foreign keys.
/// The chain of the exception is walked whatever the outer exception: EF reports a deadlock as an
/// <see cref="InvalidOperationException"/>, not a <c>DbUpdateException</c>, with the <see cref="MySqlException"/> two levels down
/// (CONTRIBUTING.md).
/// </summary>
internal static class DatabaseErrors
{
    /// <summary>A slot deleted while a booking was being written for it: the key of the booking found no slot (§1.6).</summary>
    public static bool LostTheSlot(Exception? exception) => Met(exception, MySqlErrorCode.NoReferencedRow2);

    /// <summary>A slot being deleted that a booking written in the same moment holds: the key of the booking stops it (§1.5).</summary>
    public static bool SlotWasBooked(Exception? exception) => Met(exception, MySqlErrorCode.RowIsReferenced2);

    /// <summary>
    /// A row being deleted that another row still names through its key: a slot a booking holds (above), an activity a seat taken in the
    /// same moment holds (E17).
    /// </summary>
    public static bool StillReferenced(Exception? exception) => Met(exception, MySqlErrorCode.RowIsReferenced2);

    /// <summary>A deadlock: the database rolled the whole transaction back.</summary>
    public static bool Deadlocked(Exception? exception) => Met(exception, MySqlErrorCode.LockDeadlock);

    private static bool Met(Exception? exception, params MySqlErrorCode[] codes)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is MySqlException { ErrorCode: var code } && codes.Contains(code))
            {
                return true;
            }
        }

        return false;
    }
}
