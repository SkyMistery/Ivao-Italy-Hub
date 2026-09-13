using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>
/// What waits for whoever is looking, on their dashboard (note 2026-09-13-le-dashboard-a-tutto-schermo
/// §3.5): the pages they may approve that are waiting, the contact messages nobody has opened in their
/// departments, the documents of their departments due for a look, and their own drafts.
/// <para>One block with a choice rather than four blocks, because the four are one question — "what is
/// mine to do?" — with four answers, and one provider, one component and one set of words draw them
/// all. Always live, and answered for the person asking: the rows are read past the query filter,
/// because a draft is invisible to it, and narrowed here by the same permissions the screens behind
/// the links ask for.</para>
/// </summary>
public sealed class MyWorkProvider(HubDbContext database, ICurrentUser currentUser, IClock clock) : IDataBlockProvider
{
    public const string Approvals = "approvals";
    public const string Contacts = "contacts";
    public const string Reviews = "reviews";
    public const string Drafts = "drafts";

    /// <summary>The closed set, in the order the block offers them.</summary>
    public static readonly IReadOnlyList<string> Kinds = [Approvals, Contacts, Reviews, Drafts];

    /// <summary>How many rows are read before the permissions narrow them: a dashboard is not a list screen.</summary>
    private const int Window = 200;

    public string Key => CoreBlocks.MyWork;

    public async Task<JsonNode> ResolveAsync(JsonNode? props, DataBlockContext context, CancellationToken cancellationToken)
    {
        var what = BlockProps.Text(props, "what");
        var items = new JsonArray();

        if (!currentUser.IsStaff && !currentUser.IsSuperadmin)
        {
            return new JsonObject { ["what"] = what, ["items"] = items };
        }

        var limit = DataBlockScope.LimitOf(props);

        var rows = what switch
        {
            Approvals => await ApprovalsAsync(cancellationToken),
            Contacts => await ContactsAsync(cancellationToken),
            Reviews => await ReviewsAsync(cancellationToken),
            Drafts => await DraftsAsync(cancellationToken),
            _ => [],
        };

        foreach (var row in rows.Take(limit))
        {
            items.Add(row);
        }

        return new JsonObject { ["what"] = what, ["items"] = items };
    }

    private async Task<List<JsonObject>> ApprovalsAsync(CancellationToken cancellationToken)
    {
        var waiting = await CrudSource.BackOffice<ContentEntry>(database)
            .AsNoTracking()
            .Where(content => content.Status == PublishStatus.Ready && !content.IsTemplate)
            .OrderBy(content => content.ReadyAt)
            .Take(Window)
            .ToListAsync(cancellationToken);

        return [.. waiting
            .Where(content => currentUser.Has(CorePermissions.ContentApprove, content.OwnerDepartment))
            .Select(content => Item(content.Title, $"/staff/content/{content.Id}", content.OwnerDepartment, content.ReadyAt))];
    }

    private async Task<List<JsonObject>> ContactsAsync(CancellationToken cancellationToken)
    {
        var arrived = await CrudSource.BackOffice<ContactMessage>(database)
            .AsNoTracking()
            .Where(message => message.Status == ContactStatus.New)
            .OrderByDescending(message => message.CreatedAt)
            .Take(Window)
            .ToListAsync(cancellationToken);

        return [.. arrived
            .Where(message => currentUser.Has(CorePermissions.ContactsView, message.OwnerDepartment))
            .Select(message => Item(
                message.Subject,
                $"/staff/{message.OwnerDepartment.ToString().ToLowerInvariant()}/contacts/{message.Id}",
                message.OwnerDepartment,
                message.CreatedAt))];
    }

    private async Task<List<JsonObject>> ReviewsAsync(CancellationToken cancellationToken)
    {
        var today = clock.UtcNow.Date;
        var due = await CrudSource.BackOffice<ContentEntry>(database)
            .AsNoTracking()
            .Where(content => content.Kind == ContentKind.Document
                && !content.IsTemplate
                && content.ReviewOn != null
                && content.ReviewOn <= today
                && content.RetiredAt == null)
            .OrderBy(content => content.ReviewOn)
            .Take(Window)
            .ToListAsync(cancellationToken);

        return [.. due
            .Where(content => currentUser.Has(CorePermissions.ContentEdit, content.OwnerDepartment))
            .Select(content => Item(content.Title, $"/staff/content/{content.Id}", content.OwnerDepartment, content.ReviewOn))];
    }

    private async Task<List<JsonObject>> DraftsAsync(CancellationToken cancellationToken)
    {
        var vid = currentUser.Vid;
        var mine = await CrudSource.BackOffice<ContentEntry>(database)
            .AsNoTracking()
            .Where(content => !content.IsTemplate
                && content.Status != PublishStatus.Published
                && (content.CreatedBy == vid || content.UpdatedBy == vid || content.ReadyBy == vid))
            .OrderByDescending(content => content.UpdatedAt)
            .Take(Window)
            .ToListAsync(cancellationToken);

        return [.. mine.Select(content =>
        {
            var item = Item(content.Title, $"/staff/content/{content.Id}", content.OwnerDepartment, content.UpdatedAt);
            item["status"] = content.Status.ToString();
            item["sentBack"] = content.Status == PublishStatus.Draft && content.ReviewNote is not null;
            return item;
        })];
    }

    private static JsonObject Item(Localized<string> title, string url, Department department, DateTime? at) => new()
    {
        ["title"] = BlockProps.Translated(title),
        ["url"] = url,
        ["department"] = department.ToString(),
        ["at"] = at is { } instant ? BlockProps.Instant(instant) : null,
    };

    private static JsonObject Item(string title, string url, Department department, DateTime? at) => new()
    {
        ["text"] = title,
        ["url"] = url,
        ["department"] = department.ToString(),
        ["at"] = at is { } instant ? BlockProps.Instant(instant) : null,
    };
}
