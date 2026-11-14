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
/// <para>The chain is walked whatever the outer exception: EF reports a deadlock as an <see cref="InvalidOperationException"/>, not a
/// <c>DbUpdateException</c>, with the <see cref="MySqlException"/> two levels down (CONTRIBUTING.md).</para>
/// </summary>
public static partial class WriteConflicts
{
    /// <summary>The database's own answer when another write won, or <c>null</c> when the failure is anything else.</summary>
    public static MySqlException? AnotherWriteWon(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry or MySqlErrorCode.LockDeadlock } conflict)
            {
                return conflict;
            }
        }

        return null;
    }

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

    [GeneratedRegex(@"for key '(?<key>[^']+)'", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KeyPattern();
}
