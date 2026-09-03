using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// What happens when the second pass of the interceptor is the thing that fails.
/// <para>The write itself has already succeeded by then, and the transaction it landed in is one
/// the interceptor opened for it. Without a hand on that failure the transaction was left open and
/// the entry left behind in the pending table: the caller saw an exception, the row was neither
/// committed nor rolled back, and the connection stayed poisoned for whoever picked it up next.
/// The existing <c>ProjectionUpsertedInSameTransaction</c> covers the other rollback — the one the
/// caller asks for — which is the well behaved path.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class InterceptorFailureTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int SuperadminVid = 700003;

    private readonly TestCurrentUser _user = new();
    private HubWebApplicationFactory _factory = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, currentUser: _user);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task AProjectionThatThrowsRollsBackTheWriteAndLeavesNoTransactionOpen()
    {
        var token = TestContext.Current.CancellationToken;
        _user.Superadmin(SuperadminVid);

        var slug = $"broken-{Guid.NewGuid():N}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

            // Published, so the interceptor projects it, and with a body that is not JSON at all.
            // Nothing validates the envelope before F7, so this is reachable today from a seed, a
            // migration or anything writing through the context by hand.
            database.Contents.Add(new ContentEntry
            {
                Kind = ContentKind.Page,
                Slug = slug,
                OwnerDepartment = Department.ED,
                Visibility = Visibility.Public,
                Status = PublishStatus.Published,
                Title = "Rotta".L("Broken"),
                BodyJson = "{ this is not json",
            });

            await Assert.ThrowsAnyAsync<Exception>(() => database.SaveChangesAsync(token));

            // The transaction the interceptor opened is closed, not abandoned.
            Assert.Null(database.Database.CurrentTransaction);
        }

        // And the row went with it: a write whose projection failed is not a write.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

            Assert.Null(await database.Contents.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(content => content.Slug == slug, token));
        }
    }

    [Fact]
    public async Task TheContextStillWorksAfterAProjectionHasThrown()
    {
        var token = TestContext.Current.CancellationToken;
        _user.Superadmin(SuperadminVid);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        database.Contents.Add(new ContentEntry
        {
            Kind = ContentKind.Page,
            Slug = $"broken-{Guid.NewGuid():N}",
            OwnerDepartment = Department.ED,
            Visibility = Visibility.Public,
            Status = PublishStatus.Published,
            Title = "Rotta".L("Broken"),
            BodyJson = "{ this is not json",
        });

        await Assert.ThrowsAnyAsync<Exception>(() => database.SaveChangesAsync(token));

        // The failed entity is dropped, the way a caller recovering from a failed save would.
        database.ChangeTracker.Clear();

        // A leaked pending entry used to make the next save on this context skip its own second
        // pass, or trip over a transaction that was already disposed.
        var link = new Link
        {
            OwnerDepartment = Department.ED,
            Visibility = Visibility.Public,
            Title = "Roma".L("Rome"),
            Url = "https://www.ivao.aero",
            IsActive = true,
        };

        database.Links.Add(link);
        await database.SaveChangesAsync(token);

        var projected = await database.SearchIndex.AsNoTracking()
            .Where(row => row.SourceId == $"link:{link.Id}")
            .ToListAsync(token);

        Assert.Equal(2, projected.Count);
        Assert.Null(database.Database.CurrentTransaction);
    }
}
