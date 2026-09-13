using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A page at the top of the site for a class of tests to put its own pages under.
/// <para>Since 13 September 2026 the top of the site is for whoever holds <c>Content.Approve</c>
/// (note 2026-09-13-contenuti-centralizzati, 3.7), and a coordinator of a department does not. The
/// tests that were never about the address — publication, the envelope, the templates — write their
/// pages as a coordinator, so they write them under this: seeded the way the installation seeds, with
/// nobody signed in, once per class and found again by its address on every run.</para>
/// </summary>
internal static class TestShelf
{
    public static async Task<long> SeedAsync(
        HubWebApplicationFactory factory,
        string slug,
        CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var existing = await database.Contents
            .IgnoreQueryFilters()
            .Where(row => row.Kind == ContentKind.Page && !row.IsTemplate && row.ParentPath == null && row.Slug == slug)
            .Select(row => (long?)row.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is { } id)
        {
            return id;
        }

        var shelf = new ContentEntry
        {
            Kind = ContentKind.Page,
            Slug = slug,
            OwnerDepartment = Department.WD,
            Visibility = Visibility.Staff,
            Status = PublishStatus.Draft,
            Title = new Localized<string>(
            [
                new KeyValuePair<string, string>("it", "Scaffale dei test"),
                new KeyValuePair<string, string>("en", "Test shelf"),
            ]),
        };

        database.Contents.Add(shelf);
        await database.SaveChangesAsync(cancellationToken);
        return shelf.Id;
    }
}
