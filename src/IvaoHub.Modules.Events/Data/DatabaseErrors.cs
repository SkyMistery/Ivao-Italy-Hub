using MySqlConnector;

namespace IvaoHub.Modules.Events.Data;

/// <summary>
/// What the database answered when a save of the events failed on another write of the same rows — the only failures a verb of
/// the module answers by itself; every other one surfaces as itself. The chain of the exception is walked whatever the outer
/// exception: EF reports a deadlock as an <see cref="InvalidOperationException"/>, not a <c>DbUpdateException</c>, with the
/// <see cref="MySqlException"/> two levels down (CONTRIBUTING.md).
/// </summary>
internal static class DatabaseErrors
{
    /// <summary>
    /// A key a unique index already holds — the slot of a booking (§1.6), the flight of a slot (§1.5) —, or the deadlock two inserts
    /// of one key meet in when its row was just deleted and not purged yet (CONTRIBUTING.md): in both, another write took the key.
    /// </summary>
    public static bool TookTheKey(Exception? exception) =>
        Met(exception, MySqlErrorCode.DuplicateKeyEntry, MySqlErrorCode.LockDeadlock);

    /// <summary>
    /// A key a unique index already holds, and nothing else: what a pilot's booking is told as «taken» (§1.6). A deadlock is not
    /// one — it may meet a load of the slots or «delete the free ones» as well — and is answered «try again» (the review of #233,
    /// point 3).
    /// </summary>
    public static bool Duplicated(Exception? exception) => Met(exception, MySqlErrorCode.DuplicateKeyEntry);

    /// <summary>A slot deleted while a booking was being written for it: the key of the booking found no slot (§1.6).</summary>
    public static bool LostTheSlot(Exception? exception) => Met(exception, MySqlErrorCode.NoReferencedRow2);

    /// <summary>A slot being deleted that a booking written in the same moment holds: the key of the booking stops it (§1.5).</summary>
    public static bool SlotWasBooked(Exception? exception) => Met(exception, MySqlErrorCode.RowIsReferenced2);

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
