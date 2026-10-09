using IvaoHub.Core.Data;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The queue of the mail writes each outcome as soon as it is known (note <c>decisions/2026-10-09-i-job-che-recuperano.md</c>;
/// 2026-09-28-i-job-quando-passenger-spegne-l-hub §2 point 4): a run stopped half way through its batch — the host stopping
/// an idle process — has every mail it sent written down, and the next run sends none of them again.
/// <para>The rows are for shared mailboxes (VID zero) at addresses of this test's own, so no person is seeded. The job is
/// built by hand with a sender of the test's, as <c>ContactsAndNotificationsTests</c> builds it: the host has no mail server,
/// and its own run every minute leaves the queue alone.</para>
/// </summary>
[Collection(MariaDbCollection.Name)]
public sealed class NotificationOutcomeTests(MariaDbFixture mariaDb) : IAsyncLifetime
{
    private HubWebApplicationFactory _factory = null!;
    private string[] _addresses = [];

    public ValueTask InitializeAsync()
    {
        _factory = new HubWebApplicationFactory(mariaDb.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications
                .Where(row => _addresses.Contains(row.Address))
                .ExecuteDeleteAsync(CancellationToken.None);
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AMailSentBeforeTheRunWasStoppedIsNotSentAgain()
    {
        var token = TestContext.Current.CancellationToken;

        // What other classes left waiting goes first, so that this test's mails are the next batch: their hosts have no mail
        // server, and the queue is the installation's.
        var drain = new Sender();
        while (await RunAsync(drain, token) != (0, 0))
        {
        }

        var marker = Guid.NewGuid().ToString("N")[..8];
        _addresses = [.. Enumerable.Range(1, 5).Select(index => $"e10j-{marker}-{index}@example.org")];
        await QueueAsync(_addresses, token);

        // The run is stopped as it hands over the third of these: two have gone out.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var stopped = new Sender { Marker = marker, StopAt = 3, Stop = stop };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(stopped, stop.Token));
        Assert.Equal(_addresses[..2], stopped.Sent.Where(address => address.Contains(marker, StringComparison.Ordinal)));

        // And they are written down as sent; the others wait, untried.
        var rows = await RowsAsync(token);
        Assert.All(rows.Take(2), row =>
        {
            Assert.Equal(NotificationStatus.Sent, row.Status);
            Assert.NotNull(row.SentAt);
        });
        Assert.All(rows.Skip(2), row =>
        {
            Assert.Equal(NotificationStatus.Pending, row.Status);
            Assert.Equal(0, row.Attempts);
        });

        // The next run sends the three that had not gone, and none of the two again.
        var next = new Sender();
        await RunAsync(next, token);
        Assert.Equal(_addresses[2..], next.Sent.Where(address => address.Contains(marker, StringComparison.Ordinal)));
        Assert.All(await RowsAsync(token), row => Assert.Equal(NotificationStatus.Sent, row.Status));
    }

    private async Task<(int Sent, int Failed)> RunAsync(Sender sender, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var job = new NotificationDispatchJob(
            scope.ServiceProvider.GetRequiredService<HubDbContext>(),
            sender,
            scope.ServiceProvider.GetRequiredService<LocaleCatalog>(),
            Options.Create(new SmtpOptions { Host = "localhost", From = "hub@example.org" }),
            scope.ServiceProvider.GetRequiredService<IClock>(),
            NullLogger<NotificationDispatchJob>.Instance);

        return await job.RunAsync(cancellationToken);
    }

    private async Task QueueAsync(IEnumerable<string> addresses, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;

        // One at a time, so that the queue's order — by id — is the order of the addresses.
        foreach (var address in addresses)
        {
            database.Notifications.Add(new Notification
            {
                Type = NotificationTypes.ContactReceived,
                Vid = 0,
                Address = address,
                Locale = "en",
                DataJson = """{"subject":"e10j-test","department":"e10j-test","url":"https://example.org"}""",
                CreatedAt = now,
            });

            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<List<Notification>> RowsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications.AsNoTracking()
            .Where(row => _addresses.Contains(row.Address))
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// A mail server that writes down the address of what it was handed, and is stopped on demand while it hands over one
    /// of the test's own mails — a mail some job of the host queued meanwhile passes, and is not counted.
    /// </summary>
    private sealed class Sender : IMailSender
    {
        private readonly List<string> _sent = [];

        /// <summary>What the test's own addresses contain.</summary>
        public string? Marker { get; init; }

        /// <summary>The test's own mail, counted from one, during which the run is stopped; nothing is stopped when zero.</summary>
        public int StopAt { get; init; }

        public CancellationTokenSource? Stop { get; init; }

        public IReadOnlyList<string> Sent => _sent;

        public Task SendAsync(OutgoingMail mail, CancellationToken cancellationToken = default)
        {
            var ours = Marker is not null && mail.To.Contains(Marker, StringComparison.Ordinal);
            if (ours && StopAt > 0 && _sent.Count(address => address.Contains(Marker!, StringComparison.Ordinal)) + 1 == StopAt)
            {
                // The host stops the process while this one is on its way: it never arrives.
                Stop!.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            _sent.Add(mail.To);
            return Task.CompletedTask;
        }
    }
}
