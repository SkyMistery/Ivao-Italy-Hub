using System.Collections.Concurrent;
using System.Data.Common;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// What the projections cost.
/// <para>They are written inside the transaction of the write itself, which is exactly why the cost
/// matters: every query is a round trip with a lock held. Reading what each row had projected one
/// row at a time meant three queries per row — a save of fifty rows issued a hundred and fifty —
/// so the reading is done once for the whole save instead. This pins that, because the correctness
/// of the batching is invisible until somebody puts the loop back inside.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class ProjectionBatchingTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 700601;
    private const int Rows = 12;

    private readonly TestCurrentUser _user = new();
    private readonly CommandCounter _commands = new();
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(
            mariaDb.ConnectionString,
            currentUser: _user,
            extraInterceptor: _commands);

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task OneSaveReadsTheProjectionTablesOnceHoweverManyRowsItTouches()
    {
        var token = TestContext.Current.CancellationToken;
        _user.Superadmin(SuperadminVid);

        var category = $"batch-{Guid.NewGuid():N}";

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        for (var index = 0; index < Rows; index++)
        {
            database.Links.Add(new Link
            {
                OwnerDepartment = Department.ED,
                Visibility = Visibility.Public,
                Category = category,
                Sort = index,
                Title = "Roma".L("Rome"),
                Url = "https://www.ivao.aero",
                IsActive = true,
            });
        }

        _commands.Reset();
        await database.SaveChangesAsync(token);

        // One read per projection table for the whole save, not one per row.
        Assert.Equal(1, _commands.SelectsFrom("cms_search_index"));
        Assert.Equal(1, _commands.SelectsFrom("cms_calendar_entries"));
        Assert.Equal(1, _commands.SelectsFrom("cms_award_signals"));

        // And every row is projected: batching that lost a row would be worse than the round trips.
        var sourceIds = await database.Links.AsNoTracking()
            .Where(link => link.Category == category)
            .Select(link => "link:" + link.Id)
            .ToListAsync(token);

        Assert.Equal(Rows, sourceIds.Count);

        var projected = await database.SearchIndex.AsNoTracking()
            .Where(row => sourceIds.Contains(row.SourceId) && row.Locale == "en")
            .CountAsync(token);

        Assert.Equal(Rows, projected);
    }

    /// <summary>Counts the statements the contexts of the host send, by the table they name.</summary>
    private sealed class CommandCounter : DbCommandInterceptor
    {
        private readonly ConcurrentBag<string> _texts = [];

        public void Reset() => _texts.Clear();

        public int SelectsFrom(string table) => _texts.Count(text =>
            text.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && text.Contains(table, StringComparison.OrdinalIgnoreCase));

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            _texts.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            _texts.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
