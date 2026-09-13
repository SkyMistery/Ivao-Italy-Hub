using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Content;

/// <summary>The menu entry an author proposes with a page: under which entry, and in which words.</summary>
/// <param name="ParentId">The public menu entry it goes under; null at the top of the menu.</param>
/// <param name="Label">The words, in every language.</param>
public sealed record ProposedMenuEntry(long? ParentId, Localized<string> Label);

/// <summary>What the review of a page may be asked to do.</summary>
public enum ContentReviewAction
{
    /// <summary>The author: it is ready, somebody look at it.</summary>
    Ready,

    /// <summary>The author: not ready after all, back to the draft.</summary>
    Withdraw,

    /// <summary>The approver: not this time, and why.</summary>
    SendBack,

    /// <summary>The approver: publish it, with the address and the menu entry as corrected.</summary>
    Approve,
}

/// <summary>One request to the review of a page.</summary>
/// <param name="Action">What to do.</param>
/// <param name="Note">What the author says marking it ready, or what the approver says sending it back.</param>
/// <param name="Menu">The menu entry proposed, or corrected; null for none.</param>
/// <param name="Changelog">Approving: a line for the staff about what changed.</param>
/// <param name="Slug">Approving: the last segment of the address, corrected.</param>
/// <param name="ParentId">Approving: the page it goes under, corrected; null at the top.</param>
public sealed record ContentReviewRequest(
    ContentReviewAction Action,
    string? Note = null,
    ProposedMenuEntry? Menu = null,
    string? Changelog = null,
    string? Slug = null,
    long? ParentId = null);

/// <summary>How one section of a page changed between what is online and what is ready.</summary>
public enum SectionChange
{
    Added,
    Removed,
    Changed,
    Unchanged,
}

/// <summary>One section in the summary an approver reads.</summary>
/// <param name="Key">The key of the section, or its identifier when it has none.</param>
/// <param name="Title">Its title, when it has one.</param>
/// <param name="Change">What happened to it.</param>
public sealed record SectionChangeDto(string Key, Localized<string>? Title, SectionChange Change);

/// <summary>What the approver of a page is shown before deciding (note 2026-09-13-contenuti-centralizzati, 3.2).</summary>
/// <param name="FirstPublication">True when nothing of this page is online yet.</param>
/// <param name="TitleChanged">True when the title is not the one online.</param>
/// <param name="Path">The address the page will be published at.</param>
/// <param name="Sections">Every section, in the order of the page, the removed ones after.</param>
/// <param name="Menu">The menu entry proposed with it, if any.</param>
/// <param name="Note">The last word of the review.</param>
/// <param name="ReadyAt">When it was marked ready.</param>
/// <param name="ReadyByName">Who marked it ready, as a name.</param>
public sealed record ContentReviewDto(
    bool FirstPublication,
    bool TitleChanged,
    string Path,
    IReadOnlyList<SectionChangeDto> Sections,
    ProposedMenuEntry? Menu,
    string? Note,
    DateTime? ReadyAt,
    string? ReadyByName);

/// <summary>
/// The review of a page (G19, note 2026-09-13-contenuti-centralizzati §3.2): a department marks a
/// page ready, and somebody holding <see cref="CorePermissions.ContentApprove"/> — the director and
/// the web team, or a grant — publishes it, or sends it back with a note.
/// <para>Which kinds go through it is the division's choice (<see cref="DivisionOptions.ContentApproval"/>).
/// The row is its own candidate: while it waits nobody writes it — the CRUD engine refuses — so what
/// is approved is exactly what was marked ready, without a copy of the body kept anywhere else.</para>
/// </summary>
public sealed class ContentReviewService(
    HubDbContext database,
    ContentPublishService publish,
    ContentAddresses addresses,
    INotificationService notifications,
    ICurrentUser currentUser,
    IClock clock,
    IOptions<DivisionOptions> division)
{
    /// <summary>The longest note a review carries.</summary>
    public const int MaxNoteLength = 1000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LocalizedJsonConverterFactory() },
    };

    private static readonly IReadOnlyDictionary<string, string[]> NoLocales =
        new Dictionary<string, string[]>(StringComparer.Ordinal);

    /// <summary>Whether this row is published only by approval, in this division.</summary>
    public bool RequiresApproval(ContentEntry content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return !content.IsTemplate
            && division.Value.ContentApproval.Contains(content.Kind.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Whether the person asking may approve a row of this department.</summary>
    public bool MayApprove(ContentEntry content) => currentUser.Has(CorePermissions.ContentApprove, content.OwnerDepartment);

    /// <summary>
    /// Does what the request asks, or says why it may not. The permission on the row — <c>Edit</c>
    /// for the author's two actions, <c>Approve</c> for the approver's — is the endpoint's to check.
    /// </summary>
    public async Task<ContentPublishFailure?> ActAsync(
        ContentEntry content,
        ContentReviewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Note is { Length: > MaxNoteLength })
        {
            return Refuse("note", "errors.text.tooLong");
        }

        if (!RequiresApproval(content))
        {
            return Refuse("status", "errors.content.review.notReviewed");
        }

        return request.Action switch
        {
            ContentReviewAction.Ready => await ReadyAsync(content, request, cancellationToken),
            ContentReviewAction.Withdraw => await BackToDraftAsync(content, note: null, notify: false, cancellationToken),
            ContentReviewAction.SendBack => await BackToDraftAsync(content, request.Note, notify: true, cancellationToken),
            ContentReviewAction.Approve => await ApproveAsync(content, request, cancellationToken),
            _ => Refuse("action", "errors.content.review.unknownAction"),
        };
    }

    /// <summary>What an approver is shown: the sections that changed, the address, the menu entry.</summary>
    public async Task<ContentReviewDto> DescribeAsync(ContentEntry content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var online = await publish.PublishedVersionAsync(content, cancellationToken);
        var sections = Compare(
            online is null ? null : JsonNode.Parse(online.BodyJson),
            JsonNode.Parse(content.BodyJson));

        var readyByName = content.ReadyBy is { } vid
            ? await database.Users
                .AsNoTracking()
                .Where(user => user.Vid == vid)
                .Select(user => user.FirstName + " " + user.LastName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new ContentReviewDto(
            FirstPublication: online is null,
            TitleChanged: online is not null
                && JsonSerializer.Serialize(online.Title, Json) != JsonSerializer.Serialize(content.Title, Json),
            Path: content.Url,
            Sections: sections,
            Menu: ReadMenu(content),
            Note: content.ReviewNote,
            ReadyAt: content.ReadyAt,
            ReadyByName: string.IsNullOrWhiteSpace(readyByName) ? null : readyByName.Trim());
    }

    private async Task<ContentPublishFailure?> ReadyAsync(
        ContentEntry content,
        ContentReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (content.Status == PublishStatus.Ready)
        {
            return Refuse("status", "errors.content.review.alreadyReady");
        }

        // What publication would refuse, refused now: an approver should not be asked to look at a
        // page with a language missing.
        if (await publish.ProblemsAsync(content, cancellationToken) is { } problems)
        {
            return problems;
        }

        if (request.Menu is { } menu && await MenuProblemAsync(menu, cancellationToken) is { } menuProblem)
        {
            return menuProblem;
        }

        content.Status = PublishStatus.Ready;
        content.ReadyAt = clock.UtcNow;
        content.ReadyBy = currentUser.Vid;
        content.ReviewNote = Blank(request.Note);
        content.ProposedMenuJson = request.Menu is null ? null : JsonSerializer.Serialize(request.Menu, Json);
        await database.SaveChangesAsync(cancellationToken);

        await notifications.QueueAsync(
            new NotificationIntent(
                NotificationTypes.ContentReadyForApproval,
                [.. (await ApproversAsync(content.OwnerDepartment, cancellationToken)).Select(NotificationRecipient.Member)],
                MailData(content)),
            cancellationToken);

        return null;
    }

    private async Task<ContentPublishFailure?> BackToDraftAsync(
        ContentEntry content,
        string? note,
        bool notify,
        CancellationToken cancellationToken)
    {
        if (content.Status != PublishStatus.Ready)
        {
            return Refuse("status", "errors.content.review.notReady");
        }

        var author = content.ReadyBy;

        // Back to what it was before it waited: a page with a version online is still that page.
        content.Status = content.PublishedVersionId is null ? PublishStatus.Draft : PublishStatus.Published;
        content.ReadyAt = null;
        content.ReadyBy = null;
        if (notify)
        {
            content.ReviewNote = Blank(note);
        }

        await database.SaveChangesAsync(cancellationToken);

        if (notify && author is { } vid)
        {
            var data = new Dictionary<string, string>(MailData(content), StringComparer.Ordinal)
            {
                ["note"] = content.ReviewNote ?? string.Empty,
            };

            await notifications.QueueAsync(
                new NotificationIntent(NotificationTypes.ContentSentBack, [NotificationRecipient.Member(vid)], data),
                cancellationToken);
        }

        return null;
    }

    private async Task<ContentPublishFailure?> ApproveAsync(
        ContentEntry content,
        ContentReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (content.Status != PublishStatus.Ready)
        {
            return Refuse("status", "errors.content.review.notReady");
        }

        // The address as the approver corrected it, through the one check a page written by hand
        // goes through: a small mistake is fixed here rather than sent back for a week.
        if (request.Slug is { } slug && (slug != content.Slug || request.ParentId != content.ParentId))
        {
            content.Slug = slug.Trim();
            content.ParentId = request.ParentId;

            if (await addresses.PrepareAsync(content, isNew: false, cancellationToken) is { } refused)
            {
                return new ContentPublishFailure(refused, NoLocales);
            }
        }

        var menu = request.Menu;
        if (menu is not null && await MenuProblemAsync(menu, cancellationToken) is { } menuProblem)
        {
            return menuProblem;
        }

        var author = content.ReadyBy;
        content.ReadyAt = null;
        content.ReadyBy = null;
        content.ProposedMenuJson = null;
        content.ReviewNote = null;

        if (menu is not null)
        {
            // In the same unit of work as the publication, which saves it: a page approved with a
            // menu entry is never online without it, nor the entry without the page.
            var sort = await database.MenuItems
                .Where(item => item.Scope == MenuScope.Public && item.ParentId == menu.ParentId)
                .Select(item => (int?)item.Sort)
                .MaxAsync(cancellationToken);

            database.MenuItems.Add(new MenuItem
            {
                Scope = MenuScope.Public,
                ParentId = menu.ParentId,
                Sort = (sort ?? 0) + 10,
                Label = menu.Label,
                Path = content.Url,
                Visibility = content.Visibility,
                IsActive = true,
            });
        }

        if (await publish.PublishAsync(content, Blank(request.Changelog), cancellationToken, approvedBy: currentUser.Vid) is { } failure)
        {
            return failure;
        }

        if (author is { } vid)
        {
            await notifications.QueueAsync(
                new NotificationIntent(NotificationTypes.ContentApproved, [NotificationRecipient.Member(vid)], MailData(content)),
                cancellationToken);
        }

        return null;
    }

    /// <summary>A menu entry goes under an entry of the public menu that sits at the top, and has words.</summary>
    private async Task<ContentPublishFailure?> MenuProblemAsync(ProposedMenuEntry menu, CancellationToken cancellationToken)
    {
        if (!menu.Label.HasAll(division.Value.Locales))
        {
            return new ContentPublishFailure(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["menu.label"] = ["errors.localized.missing"] },
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["menu.label"] = [.. division.Value.Locales.Where(locale => string.IsNullOrWhiteSpace(menu.Label.Get(locale)))],
                });
        }

        if (menu.ParentId is { } parentId
            && !await CrudSource.BackOffice<MenuItem>(database)
                .AnyAsync(item => item.Id == parentId && item.Scope == MenuScope.Public && item.ParentId == null, cancellationToken))
        {
            return Refuse("menu.parentId", "errors.content.review.menuParent");
        }

        return null;
    }

    /// <summary>
    /// Who may approve a page of this department: every position that reaches every department —
    /// the director and the web team — and every live grant of <c>Content.Approve</c> on it or on all.
    /// </summary>
    private async Task<IReadOnlyList<int>> ApproversAsync(Department department, CancellationToken cancellationToken)
    {
        var settings = division.Value;
        var firs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var positions = await database.UserStaffPositions
            .AsNoTracking()
            .Select(position => new { position.Vid, position.Position })
            .ToListAsync(cancellationToken);

        var byRole = positions
            .Where(position => StaffRoleMap.Parse(position.Position, settings.Code, firs) is { } parsed
                && RolePermissionMatrix.ReachesEveryDepartment(parsed))
            .Select(position => position.Vid);

        var now = clock.UtcNow;
        var byGrant = await database.UserGrants
            .AsNoTracking()
            .Where(grant => grant.Kind == GrantKind.Permission
                && grant.Value == CorePermissions.ContentApprove
                && grant.Effect == GrantEffect.Grant
                && grant.SuspendedAt == null
                && (grant.ExpiresAt == null || grant.ExpiresAt > now)
                && (grant.Department == null || grant.Department == department))
            .Select(grant => grant.Vid)
            .ToListAsync(cancellationToken);

        return [.. byRole.Concat(byGrant).Distinct()];
    }

    private Dictionary<string, string> MailData(ContentEntry content) => new(StringComparer.Ordinal)
    {
        ["department"] = content.OwnerDepartment.ToString(),
        ["title"] = content.Title.Get(division.Value.DefaultLocale) ?? content.Slug,
        ["path"] = content.Url,
        ["url"] = $"https://{division.Value.Domain}/staff/content/{content.Id}",
    };

    private static ProposedMenuEntry? ReadMenu(ContentEntry content) =>
        content.ProposedMenuJson is null ? null : JsonSerializer.Deserialize<ProposedMenuEntry>(content.ProposedMenuJson, Json);

    /// <summary>
    /// The sections of the two bodies, matched by key — which a copy of a template keeps — or by
    /// identifier. What is compared is the whole section as JSON: the blocks' properties are never
    /// read, only whether the section is the same text it was.
    /// </summary>
    private static IReadOnlyList<SectionChangeDto> Compare(JsonNode? online, JsonNode? ready)
    {
        var before = Sections(online);
        var after = Sections(ready);
        var changes = new List<SectionChangeDto>();

        foreach (var (key, section) in after)
        {
            var title = Title(section);
            changes.Add(new SectionChangeDto(
                key,
                title,
                !before.TryGetValue(key, out var previous)
                    ? online is null ? SectionChange.Unchanged : SectionChange.Added
                    : Hash(previous) == Hash(section) ? SectionChange.Unchanged : SectionChange.Changed));
        }

        foreach (var (key, section) in before)
        {
            if (!after.ContainsKey(key))
            {
                changes.Add(new SectionChangeDto(key, Title(section), SectionChange.Removed));
            }
        }

        return changes;
    }

    private static Dictionary<string, JsonNode> Sections(JsonNode? body)
    {
        var sections = new Dictionary<string, JsonNode>(StringComparer.Ordinal);

        if (body?["sections"] is JsonArray array)
        {
            foreach (var section in array.OfType<JsonObject>())
            {
                var key = section["key"]?.GetValue<string>() ?? section["id"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(key))
                {
                    sections.TryAdd(key, section);
                }
            }
        }

        return sections;
    }

    private static Localized<string>? Title(JsonNode section)
    {
        if (section["title"] is not JsonObject title)
        {
            return null;
        }

        var values = title
            .Where(pair => pair.Value is JsonValue value && value.TryGetValue<string>(out _))
            .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value!.GetValue<string>()))
            .ToList();

        return values.Count == 0 ? null : new Localized<string>(values);
    }

    private static string Hash(JsonNode node) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(node.ToJsonString())));

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static ContentPublishFailure Refuse(string field, string key) =>
        new(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [key] }, NoLocales);
}
