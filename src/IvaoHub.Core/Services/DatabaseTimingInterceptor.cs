using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IvaoHub.Core.Services;

/// <summary>
/// Adds every connection opened and every command sent by a context to <see cref="RequestTiming.Here"/> (note
/// 2026-10-18-i-tempi-delle-richieste). Attached the way the contexts already take a diagnostic: registered as an
/// <see cref="IInterceptor"/>, which <c>HubDbContextServiceCollectionExtensions</c> adds to every context it builds.
/// </summary>
/// <remarks>
/// A reader is counted when it is executed and timed when it is disposed, so that its time includes reading the rows: EF
/// reads a query's rows after the execution, and the execution alone says only when the first one was ready. It reads the
/// durations EF already measured, and never the command's text.
/// </remarks>
public sealed class DatabaseTimingInterceptor : DbConnectionInterceptor, IDbCommandInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        RequestTiming.Here.Opened(eventData.Duration);
    }

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        RequestTiming.Here.Opened(eventData.Duration);
        return Task.CompletedTask;
    }

    public DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        RequestTiming.Here.Command();
        return result;
    }

    public ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        RequestTiming.Here.Command();
        return ValueTask.FromResult(result);
    }

    public InterceptionResult DataReaderDisposing(DbCommand command, DataReaderDisposingEventData eventData, InterceptionResult result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        RequestTiming.Here.CommandTook(eventData.Duration);
        return result;
    }

    public int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Executed(eventData);
        return result;
    }

    public ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Executed(eventData);
        return ValueTask.FromResult(result);
    }

    public object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Executed(eventData);
        return result;
    }

    public ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        Executed(eventData);
        return ValueTask.FromResult(result);
    }

    public void CommandFailed(DbCommand command, CommandErrorEventData eventData) => Failed(eventData);

    public Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Failed(eventData);
        return Task.CompletedTask;
    }

    private static void Executed(CommandExecutedEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        var here = RequestTiming.Here;
        here.Command();
        here.CommandTook(eventData.Duration);
    }

    private static void Failed(CommandErrorEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        var here = RequestTiming.Here;
        here.Command();
        here.CommandTook(eventData.Duration);
    }
}
