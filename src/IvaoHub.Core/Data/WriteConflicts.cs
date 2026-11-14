using MySqlConnector;

namespace IvaoHub.Core.Data;

/// <summary>
/// What the database answers when two writes of the same rows met: a key a unique index already holds, which the other write took
/// between a check and the insert, or a deadlock, where the database rolled one transaction back. Both mean what a stale row version
/// means — somebody else saved first — and the API answers them alike, 409 (<c>DomainExceptionHandler</c>; #321, point 4).
/// <para>Two publications of one page computing the same next version, two new pages taking one free address: a check before the
/// insert cannot close that gap, the unique index does, and this is how its refusal reaches the client as a conflict and not as a
/// fault.</para>
/// <para>The chain is walked whatever the outer exception: EF reports a deadlock as an <see cref="InvalidOperationException"/>, not a
/// <c>DbUpdateException</c>, with the <see cref="MySqlException"/> two levels down (CONTRIBUTING.md).</para>
/// </summary>
public static class WriteConflicts
{
    public static bool AnotherWriteWon(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry or MySqlErrorCode.LockDeadlock })
            {
                return true;
            }
        }

        return false;
    }
}
