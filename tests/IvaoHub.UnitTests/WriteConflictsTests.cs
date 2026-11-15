using System.Reflection;
using IvaoHub.Core.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The core's one answer to «did another write take this key?» (#499): the wide question takes the deadlock too, the narrow one
/// only the duplicate key; both walk the whole chain and read the error code, never the words of the message.
/// </summary>
public sealed class WriteConflictsTests
{
    private const string DuplicateMessage = "Duplicate entry '704798' for key 'ix_hub_users_discord_id'";

    [Fact]
    public void ADuplicateKeyUnderASaveAnswersBothQuestions()
    {
        var failure = new DbUpdateException("An error occurred while saving the entity changes.", Database(MySqlErrorCode.DuplicateKeyEntry, DuplicateMessage));

        Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, WriteConflicts.AnotherWriteWon(failure)?.ErrorCode);
        Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, WriteConflicts.DuplicateKey(failure)?.ErrorCode);
    }

    [Fact]
    public void ADuplicateKeyTwoLevelsDownIsStillFound()
    {
        // A copy that looked one level down only (DiscordLinks before #499) missed this one.
        var failure = Transient(Database(MySqlErrorCode.DuplicateKeyEntry, DuplicateMessage));

        Assert.NotNull(WriteConflicts.DuplicateKey(failure));
        Assert.NotNull(WriteConflicts.AnotherWriteWon(failure));
    }

    [Fact]
    public void ADeadlockTwoLevelsDownInATransientFailureIsAnotherWriteButNotADuplicateKey()
    {
        // How EF reports a MariaDB deadlock: an InvalidOperationException, not a DbUpdateException (CONTRIBUTING.md).
        var failure = Transient(Database(MySqlErrorCode.LockDeadlock, "Deadlock found when trying to get lock; try restarting transaction"));

        Assert.Equal(MySqlErrorCode.LockDeadlock, WriteConflicts.AnotherWriteWon(failure)?.ErrorCode);
        Assert.Null(WriteConflicts.DuplicateKey(failure));
        Assert.Equal(MySqlErrorCode.LockDeadlock, WriteConflicts.Deadlock(failure)?.ErrorCode);
    }

    [Fact]
    public void ADuplicateKeyIsNotADeadlock()
    {
        // The bookings, the slots' generation and the review answer a deadlock alone (#519): a duplicate key there is a fault.
        var failure = Transient(Database(MySqlErrorCode.DuplicateKeyEntry, DuplicateMessage));

        Assert.Null(WriteConflicts.Deadlock(failure));
        Assert.Null(WriteConflicts.Deadlock(new DbUpdateException("save", Database(MySqlErrorCode.NoReferencedRow2, "Cannot add or update a child row"))));
        Assert.Null(WriteConflicts.Deadlock(null));
    }

    [Fact]
    public void TheWordsOfTheMessageDoNotMakeADuplicateKey()
    {
        // The copies of Training and the tours matched «Duplicate» in the message; the code is what the database means.
        var failure = new DbUpdateException("save", Database(MySqlErrorCode.DuplicateFieldName, "Duplicate column name 'vid'"));

        Assert.Null(WriteConflicts.DuplicateKey(failure));
        Assert.Null(WriteConflicts.AnotherWriteWon(failure));
    }

    [Fact]
    public void AnyOtherFailureIsNeither()
    {
        var foreignKey = new DbUpdateException("save", Database(MySqlErrorCode.NoReferencedRow2, "Cannot add or update a child row"));

        Assert.Null(WriteConflicts.AnotherWriteWon(foreignKey));
        Assert.Null(WriteConflicts.DuplicateKey(foreignKey));
        Assert.Null(WriteConflicts.AnotherWriteWon(new InvalidOperationException("no database here")));
        Assert.Null(WriteConflicts.DuplicateKey(null));
    }

    [Fact]
    public void TheKeyIsReadWithoutTheValue()
    {
        var conflict = Database(MySqlErrorCode.DuplicateKeyEntry, DuplicateMessage);

        Assert.Equal("ix_hub_users_discord_id", WriteConflicts.KeyOf(conflict));
        Assert.Null(WriteConflicts.KeyOf(Database(MySqlErrorCode.LockDeadlock, "Deadlock found when trying to get lock")));
    }

    /// <summary>EF's transient failure, with the database's answer two levels down.</summary>
    private static InvalidOperationException Transient(MySqlException inner) =>
        new("An exception has been raised that is likely due to a transient failure.", new DbUpdateException("save", inner));

    /// <summary>
    /// The driver's own exception: its constructors are internal, so a test builds one through the one taking a code and a message, as
    /// the driver does when the server answers.
    /// </summary>
    private static MySqlException Database(MySqlErrorCode code, string message)
    {
        var constructor = typeof(MySqlException).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            [typeof(MySqlErrorCode), typeof(string)])
            ?? throw new InvalidOperationException("MySqlConnector no longer has MySqlException(MySqlErrorCode, string).");

        return (MySqlException)constructor.Invoke([code, message]);
    }
}
