using System.Text.RegularExpressions;
using MySqlConnector;

namespace IvaoHub.Core.Data;

/// <summary>
/// What the database answers when two writes of the same rows met: a key a unique index already holds, which the other write took
/// between a check and the insert, or a deadlock, where the database rolled one transaction back. Both mean what a stale row version
/// means — somebody else saved first — and the API answers them alike, 409 (<c>DomainExceptionHandler</c>; #321, point 4).
/// <para>Two publications of one page computing the same next version, two new pages taking one free address: a check before the
/// insert cannot close that gap, the unique index does, and this is how its refusal reaches the client as a conflict and not as a
/// fault.</para>
/// <para>Three questions, the wide one and two narrow ones, and every caller in the core and in the modules asks one of these and never
/// a copy (#499, #519). <see cref="AnotherWriteWon"/> takes the deadlock too: a write that only has to be tried again, or answered 409.
/// <see cref="DuplicateKey"/> takes the key alone: a write that tells the person «somebody else holds this» — a seat, an account, a
/// claim — where a deadlock is not that news, since it may meet any other write of the same rows (the review of #233, point 3).
/// <see cref="Deadlock"/> takes the deadlock alone: a place where no duplicate key is expected — no unique index can refuse the write,
/// or the key was already answered further in — so two writers locking the same rows is the one way another write wins there: a report
/// taken twice at once, a booking, the slots generated twice. A duplicate key reaching it is a fault, not news.</para>
/// <para>The chain is walked whatever the outer exception: EF reports a deadlock as an <see cref="InvalidOperationException"/>, not a
/// <c>DbUpdateException</c>, with the <see cref="MySqlException"/> two levels down (CONTRIBUTING.md). The database's error code is
/// read, never the words of its message.</para>
/// </summary>
public static partial class WriteConflicts
{
    /// <summary>
    /// The database's own answer when another write won — a duplicate key or a deadlock —, or <c>null</c> when the failure is
    /// anything else.
    /// </summary>
    public static MySqlException? AnotherWriteWon(Exception? exception) =>
        Find(exception, MySqlErrorCode.DuplicateKeyEntry, MySqlErrorCode.LockDeadlock);

    /// <summary>
    /// The database's answer when another write took the key, and nothing else: a duplicate key, or <c>null</c> — a deadlock
    /// included, which the caller lets through or answers as «try again».
    /// </summary>
    public static MySqlException? DuplicateKey(Exception? exception) => Find(exception, MySqlErrorCode.DuplicateKeyEntry);

    /// <summary>
    /// The database's answer when it rolled the transaction back on a deadlock, and nothing else: a deadlock, or <c>null</c> — a
    /// duplicate key included, which the caller lets through as the fault it is there.
    /// </summary>
    public static MySqlException? Deadlock(Exception? exception) => Find(exception, MySqlErrorCode.LockDeadlock);

    /// <summary>
    /// The name of the unique index a duplicate key met, read off the database's message (<c>… for key 'name'</c>), or <c>null</c>.
    /// Only the name: the message carries the value too, and a value may be a person's data, which a log does not keep.
    /// </summary>
    public static string? KeyOf(MySqlException conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        var match = KeyPattern().Match(conflict.Message);
        return match.Success ? match.Groups["key"].Value : null;
    }

    private static MySqlException? Find(Exception? exception, MySqlErrorCode first, MySqlErrorCode? second = null)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is MySqlException found && (found.ErrorCode == first || found.ErrorCode == second))
            {
                return found;
            }
        }

        return null;
    }

    [GeneratedRegex(@"for key '(?<key>[^']+)'", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KeyPattern();
}
