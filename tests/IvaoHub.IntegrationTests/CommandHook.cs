using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// One action of a test, run once just before a reading command of the host that it picks: something that happens between
/// two reads of the same request — a report decided between two pages of a queue — or a call that does not answer in time
/// inside one of them. The commands the action sends itself pass through here too, and find the hook already spent.
/// </summary>
public sealed class CommandHook : DbCommandInterceptor
{
    private Func<DbCommand, bool>? _matches;
    private Func<Task>? _action;
    private int _skip;

    /// <summary>Whether the action ran: a test whose hook never met its command proves nothing.</summary>
    public bool Fired { get; private set; }

    /// <summary>Runs <paramref name="action"/> before the command that matches, after letting <paramref name="skip"/> of them go.</summary>
    public void Before(Func<DbCommand, bool> matches, Func<Task> action, int skip = 0)
    {
        Fired = false;
        _skip = skip;
        _action = action;
        _matches = matches;
    }

    public void Clear()
    {
        _matches = null;
        _action = null;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (_matches is { } matches && matches(command) && Interlocked.Decrement(ref _skip) < 0
            && Interlocked.Exchange(ref _action, null) is { } action)
        {
            _matches = null;
            Fired = true;
            await action();
        }

        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
