using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The files of the library with an end (M2, T4, note 2026-09-15-file-con-scadenza §5): a row of a
/// module says until when it needs a file, a delete by hand respects it, the library shows the date,
/// and the job deletes a file only once every use of it has ended — with the very delete of the
/// library, file and row alike.
/// <para>The uses come from rows of the test module, through the projection, as a tour will write
/// them: nothing here writes <c>cms_media_uses</c> by hand.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class MediaExpiryTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private const int WebCoordinatorVid = 780032;

    private HubWebApplicationFactory _factory = null!;
    private string _mediaRoot = null!;
    private readonly List<long> _events = [];

    public ValueTask InitializeAsync()
    {
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"ivaohub-media-{Guid.NewGuid():N}");
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString, mediaDirectory: _mediaRoot);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        // The rows of the module go, and their uses with them through the projection.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            module.Events.RemoveRange(await module.Events.Where(row => _events.Contains(row.Id)).ToListAsync());
            await module.SaveChangesAsync();
        }

        await _factory.DisposeAsync();

        if (Directory.Exists(_mediaRoot))
        {
            Directory.Delete(_mediaRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AFileIsDeletedOnlyOnceEveryUseOfItHasEnded()
    {
        var token = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;

        var halfEnded = await StoreAsync(token);
        var ended = await StoreAsync(token);
        var neverDeclared = await StoreAsync(token);

        // One use ended and one not: somebody still needs it.
        await UseAsync(halfEnded, now.AddDays(-2), token);
        await UseAsync(halfEnded, now.AddDays(20), token);

        // Every use ended.
        await UseAsync(ended, now.AddDays(-40), token);
        await UseAsync(ended, now.AddDays(-2), token);

        await RunJobAsync(token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var kept = await RowAsync(hub, halfEnded.Id, token);
        Assert.Null(kept.DeletedAt);
        Assert.True(File.Exists(PathOf(halfEnded)));
        Assert.Equal(2, await hub.MediaUses.CountAsync(use => use.MediaId == halfEnded.Id, token));

        // Deleted as the library deletes: the row stays, marked, and the bytes leave the disk.
        var gone = await RowAsync(hub, ended.Id, token);
        Assert.NotNull(gone.DeletedAt);
        Assert.False(gone.HasFile);
        Assert.False(File.Exists(PathOf(ended)));
        Assert.Equal(0, await hub.MediaUses.CountAsync(use => use.MediaId == ended.Id, token));

        // Never declared by a module: not the job's business, however old.
        var untouched = await RowAsync(hub, neverDeclared.Id, token);
        Assert.Null(untouched.DeletedAt);
        Assert.True(File.Exists(PathOf(neverDeclared)));

        var idOfEnded = ended.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // By nobody, in the audit log, and a line of its own in the log of the jobs.
        Assert.True(await hub.AuditLog.AnyAsync(
            entry => entry.Entity == "cms_media" && entry.EntityId == idOfEnded && entry.Vid == 0 && entry.Action == "updated",
            token));

        var run = await hub.JobsLog.AsNoTracking()
            .Where(entry => entry.Job == MediaExpiryJob.JobName)
            .OrderByDescending(entry => entry.Id)
            .FirstAsync(token);
        Assert.Equal("succeeded", run.Status);
    }

    [Fact]
    public async Task AFileARowStillNeedsIsNotDeletedByHandAndTheLibrarySaysWhenItWillGo()
    {
        var token = TestContext.Current.CancellationToken;
        await SeedUserAsync(WebCoordinatorVid, "IT-WM", token);
        using var web = _factory.CreateApiClient();
        await _factory.SignInAsync(web, WebCoordinatorVid, token);

        var until = DateTime.UtcNow.Date.AddDays(45);
        var banner = await StoreAsync(token);
        var sample = await UseAsync(banner, until, token);

        using (var refused = await DeleteAsync(web, banner.Id, token))
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            var problem = await refused.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.Equal("errors.media.inUseByModule", problem.GetProperty("errors").GetProperty("id")[0].GetString());
        }

        Assert.Equal(until, await DeletesOnAsync(web, banner.Id, token));

        // The tour is extended: the date in the library moves with it.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            var row = await module.Events.FirstAsync(candidate => candidate.Id == sample, token);
            row.BannerNeededUntil = until.AddDays(10);
            await module.SaveChangesAsync(token);
        }

        Assert.Equal(until.AddDays(10), await DeletesOnAsync(web, banner.Id, token));

        // Once the row lets it go, the file is an ordinary file again: no date, and deleted by hand.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            var row = await module.Events.FirstAsync(candidate => candidate.Id == sample, token);
            row.BannerMediaId = null;
            await module.SaveChangesAsync(token);
        }

        Assert.Null(await DeletesOnAsync(web, banner.Id, token));

        using var deleted = await DeleteAsync(web, banner.Id, token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    private async Task RunJobAsync(CancellationToken token)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MediaExpiryJob>().RunAsync(token);
    }

    /// <summary>A file of the web department, written the way an upload writes one.</summary>
    private async Task<MediaAsset> StoreAsync(CancellationToken token)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<MediaStorage>();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var bytes = Png();
        await using var content = new MemoryStream(bytes);
        var stored = await storage.SaveAsync(content, new MediaFormat("image/png", ".png"), clock.UtcNow, token);

        var media = new MediaAsset
        {
            OwnerDepartment = Department.WD,
            Visibility = Visibility.Staff,
            FileName = "fo-test-banner.png",
            StoredName = stored.StoredName,
            Sha256 = stored.Sha256,
            ContentType = "image/png",
            ByteSize = bytes.Length,
            Width = 10,
            Height = 10,
        };

        hub.Media.Add(media);
        await hub.SaveChangesAsync(token);
        return media;
    }

    /// <summary>A row of the test module that shows the file until then; returns the row.</summary>
    private async Task<long> UseAsync(MediaAsset media, DateTime until, CancellationToken token)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var module = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        var sample = new SampleEvent
        {
            Title = $"fo-test-media-{Guid.NewGuid():N}",
            StartsAt = until.AddDays(-30),
            Status = PublishStatus.Published,
            BannerMediaId = media.Id,
            BannerNeededUntil = until,
        };

        module.Events.Add(sample);
        await module.SaveChangesAsync(token);
        _events.Add(sample.Id);
        return sample.Id;
    }

    private static Task<MediaAsset> RowAsync(HubDbContext hub, long id, CancellationToken token) =>
        hub.Media.IgnoreQueryFilters().AsNoTracking().FirstAsync(media => media.Id == id, token);

    private string PathOf(MediaAsset media)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<MediaStorage>().Find(media.StoredName)?.FullName
            ?? Path.Combine(_mediaRoot, "missing", media.StoredName);
    }

    private static async Task<DateTime?> DeletesOnAsync(HttpClient client, long id, CancellationToken token)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(
            $"{MediaEndpoints.Pattern}?pageSize=100&filter[ownerDepartment]=WD",
            token);

        var row = page.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetInt64() == id);
        return row.GetProperty("deletesOn").ValueKind == JsonValueKind.Null
            ? null
            : row.GetProperty("deletesOn").GetDateTime().ToUniversalTime();
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, long id, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri($"{MediaEndpoints.Pattern}/{id}", UriKind.Relative));
        request.Headers.Add("X-Requested-With", "hub");
        return await client.SendAsync(request, token);
    }

    private async Task SeedUserAsync(int vid, string position, CancellationToken token)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = await database.Users.FirstOrDefaultAsync(row => row.Vid == vid, token);
        if (user is null)
        {
            user = new HubUser { Vid = vid, CreatedAt = clock.UtcNow };
            database.Users.Add(user);
        }

        user.FirstName = "Test";
        user.LastName = "User";
        user.IsStaff = true;
        user.SecurityStamp = SuperadminService.NewStamp();
        user.UpdatedAt = clock.UtcNow;

        if (!await database.UserStaffPositions.AnyAsync(row => row.Vid == vid && row.Position == position, token))
        {
            var parsed = StaffRoleMap.Parse(position, "IT", new HashSet<string>());
            database.UserStaffPositions.Add(new UserStaffPosition
            {
                Vid = vid,
                Position = position,
                Department = parsed?.Department,
                Level = parsed?.Level,
                Fir = parsed?.Fir,
                SyncedAt = clock.UtcNow,
            });
        }

        await database.SaveChangesAsync(token);
    }

    /// <summary>A PNG header and sixteen random bytes: two calls never make the same file.</summary>
    private static byte[] Png()
    {
        var bytes = new byte[33 + 16];
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes);

        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 13);
        Encoding.ASCII.GetBytes("IHDR").CopyTo(bytes, 12);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), 10);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), 10);
        Guid.NewGuid().ToByteArray().CopyTo(bytes, 33);

        return bytes;
    }
}
