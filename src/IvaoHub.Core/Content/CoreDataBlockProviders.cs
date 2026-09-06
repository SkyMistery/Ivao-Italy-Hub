using System.Linq.Expressions;
using System.Text.Json.Nodes;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Content;

/// <summary>
/// Narrowing a provider's query to what the page keeping the answer may show.
/// <para>The global query filter has already said what <b>this reader</b> may see. This says what
/// may be <b>copied into</b> a page read by somebody else, which is a different question and the
/// one <see cref="VisibilityCeiling"/> answers (design M0 section 5.5). Live, there is no page and
/// nothing to narrow.</para>
/// <para>The predicate is built rather than written, for the same reason
/// <c>VisibilityQueryFilter</c> builds its own: the two properties are declared by interfaces, and
/// a lambda over an interface is a lambda the database provider has to guess at. Naming them
/// through <see cref="Expression.Property(Expression, string)"/> resolves them on the entity
/// itself, so this is one method instead of one per table.</para>
/// </summary>
public static class DataBlockScope
{
    public static IQueryable<TEntity> WithinPage<TEntity>(this IQueryable<TEntity> query, DataBlockContext context)
        where TEntity : class, IVisible, IOwnedByDepartment
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Page is not { } page)
        {
            return query;
        }

        var embeddable = VisibilityCeiling.For(page.Visibility).ToList();

        var row = Expression.Parameter(typeof(TEntity), "row");
        var visibility = Expression.Property(row, nameof(IVisible.Visibility));
        var department = Expression.Property(row, nameof(IOwnedByDepartment.OwnerDepartment));

        var allowed = Expression.Call(
            Expression.Constant(embeddable),
            typeof(List<Visibility>).GetMethod(nameof(List<Visibility>.Contains), [typeof(Visibility)])!,
            visibility);

        // "Visible to a department" means a different set of people for each one, so a row of one
        // may not travel into a page of another even though the two share a visibility.
        var ofThisDepartment = Expression.OrElse(
            Expression.NotEqual(visibility, Expression.Constant(Visibility.Department, visibility.Type)),
            Expression.Equal(department, Expression.Constant(page.Department, department.Type)));

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(
            Expression.AndAlso(allowed, ofThisDepartment),
            row));
    }

    /// <summary>Never more than this, whatever a block asks for: a block is not an export.</summary>
    public const int MaxItems = 50;

    /// <summary>What a block that does not say gets.</summary>
    public const int DefaultLimit = 10;

    /// <summary>How many rows a block may have, read from its properties.</summary>
    public static int LimitOf(JsonNode? props) =>
        Math.Clamp(BlockProps.Number(props, "limit") ?? DefaultLimit, 1, MaxItems);

    /// <summary>An answer with nothing in it, which is what a property nobody recognises earns.</summary>
    public static JsonNode Nothing(string name) => new JsonObject { [name] = new JsonArray() };
}

/// <summary>
/// The figures of the division: how many members it knows, how many of them are staff, how much it
/// has published, what is coming up.
/// <para>The set is <b>closed</b> and it is this list (design M1 section 1.2, correction 2). There
/// is no register of metrics: a module that wants a figure of its own registers a block of its own,
/// which is what plan section 9.7 already prescribes for data blocks. A register would be a second
/// registry next to the one that exists, to do the same thing with one more level in it.</para>
/// <para>A name nobody here declares is left out of the answer rather than refused: a body written
/// by a newer release must not turn into an error page on an older one.</para>
/// </summary>
public sealed class StatsProvider(HubDbContext database, IClock clock) : IDataBlockProvider
{
    public const string KnownMembers = "knownMembers";
    public const string StaffMembers = "staffMembers";
    public const string PublishedNews = "publishedNews";
    public const string PublishedDocuments = "publishedDocuments";
    public const string UpcomingEntries = "upcomingEntries";

    /// <summary>
    /// Every metric of the core, in the order the block offers them. Spelled as compound words on
    /// purpose: every string inside <c>props</c> is concatenated into the text of the page for the
    /// search index, and a metric called "news" would be a page that answers a search for news
    /// (design M1 section 1.5).
    /// </summary>
    public static readonly IReadOnlyList<string> Metrics =
        [KnownMembers, StaffMembers, PublishedNews, PublishedDocuments, UpcomingEntries];

    public string Key => CoreBlocks.Stats;

    public async Task<JsonNode> ResolveAsync(
        JsonNode? props,
        DataBlockContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var asked = BlockProps.Entries(props, "metrics", "metric")
            .Where(Metrics.Contains)
            .Distinct(StringComparer.Ordinal);

        var metrics = new JsonArray();
        foreach (var metric in asked)
        {
            metrics.Add(new JsonObject
            {
                ["metric"] = metric,
                ["value"] = await CountAsync(metric, context, cancellationToken),
            });
        }

        return new JsonObject { ["metrics"] = metrics };
    }

    private Task<int> CountAsync(string metric, DataBlockContext context, CancellationToken cancellationToken)
    {
        // Read once into a local: the database can compare a column with a value, not with a call
        // to a service it has never heard of.
        var now = clock.UtcNow;

        return metric switch
        {
            // The roster of the hub is whoever has signed in at least once: there is no endpoint
            // anywhere that lists the members of a division (plan section 16.13).
            KnownMembers => database.Users.AsNoTracking().CountAsync(cancellationToken),
            StaffMembers => database.Users.AsNoTracking().Where(user => user.IsStaff).CountAsync(cancellationToken),

            // No IgnoreQueryFilters anywhere below: "published" and "visible to this reader" are
            // both already in the filter, and the ceiling is the second question, asked once.
            PublishedNews => ContentCount(ContentKind.News, context).CountAsync(cancellationToken),
            PublishedDocuments => ContentCount(ContentKind.Document, context).CountAsync(cancellationToken),

            UpcomingEntries => database.CalendarEntries
                .AsNoTracking()
                .Where(entry => entry.StartsAtUtc >= now)
                .WithinPage(context)
                .CountAsync(cancellationToken),

            _ => Task.FromResult(0),
        };
    }

    private IQueryable<ContentEntry> ContentCount(ContentKind kind, DataBlockContext context) =>
        database.Contents
            .AsNoTracking()
            .Where(content => content.Kind == kind && !content.IsTemplate)
            .WithinPage(context);
}

/// <summary>
/// Who is connected, right now. The only block of the set that is <b>always live</b>: a captured
/// picture of the network is an expired figure passed off as the present one (plan section 9.3),
/// and the rule is in the type — <c>AlwaysLive</c> on the descriptor — rather than in an
/// <c>if</c> anywhere (design M1 section 1.5).
/// <para>Nothing here reads the database or the visibility filter, because none of this belongs to
/// anybody: it is the same picture the network shows on its own site. What the hub adds is which
/// half of it is <i>ours</i>, and that comes from the snapshot of the reference data, never from a
/// list in the code.</para>
/// </summary>
public sealed class NetworkStatsProvider(IIvaoApiClient network, IFirDirectory airspace) : IDataBlockProvider
{
    /// <summary>Controllers working a station of the division.</summary>
    public const string DivisionAtc = "divisionAtc";

    /// <summary>Pilots whose flight plan starts or ends in it.</summary>
    public const string DivisionPilots = "divisionPilots";

    /// <summary>Controllers connected anywhere on the network.</summary>
    public const string NetworkAtc = "networkAtc";

    /// <summary>Pilots connected anywhere on the network.</summary>
    public const string NetworkPilots = "networkPilots";

    /// <summary>The closed set, in the order the block offers them.</summary>
    public static readonly IReadOnlyList<string> Figures = [DivisionAtc, DivisionPilots, NetworkAtc, NetworkPilots];

    public string Key => CoreBlocks.NetworkStats;

    public async Task<JsonNode> ResolveAsync(
        JsonNode? props,
        DataBlockContext context,
        CancellationToken cancellationToken)
    {
        var area = await airspace.GetAirspaceAsync(cancellationToken);
        var status = await network.GetNetworkStatusAsync(area, cancellationToken);

        var figures = new JsonArray();
        foreach (var figure in BlockProps.Entries(props, "figures", "figure")
            .Where(Figures.Contains)
            .Distinct(StringComparer.Ordinal))
        {
            figures.Add(new JsonObject { ["figure"] = figure, ["value"] = ValueOf(status, figure) });
        }

        var positions = new JsonArray();
        if (BlockProps.Flag(props, "showPositions"))
        {
            foreach (var position in status.Positions)
            {
                positions.Add(new JsonObject
                {
                    ["callsign"] = position.Callsign,
                    ["station"] = position.Station,
                    ["frequency"] = position.Frequency,
                });
            }
        }

        return new JsonObject
        {
            // Null says the answer could not be had at all, which is a different thing from
            // nobody being connected, and the block says so rather than drawing four zeroes.
            ["updatedAt"] = status.UpdatedAt is { } instant ? BlockProps.Instant(instant) : null,
            ["figures"] = figures,
            ["positions"] = positions,
        };
    }

    private static int ValueOf(IvaoNetworkStatus status, string figure) => figure switch
    {
        DivisionAtc => status.AreaAtc,
        DivisionPilots => status.AreaPilots,
        NetworkAtc => status.NetworkAtc,
        NetworkPilots => status.NetworkPilots,
        _ => 0,
    };
}

/// <summary>
/// What is coming up, out of the one calendar of the division (plan section 9.5). Entries written
/// by the staff and entries projected by a module are the same rows and are read the same way:
/// which module wrote one is not something a page has to care about.
/// </summary>
public sealed class CalendarBlockProvider(HubDbContext database, IClock clock) : IDataBlockProvider
{
    /// <summary>From now on, however far.</summary>
    public const string Upcoming = "upcoming";

    /// <summary>The next seven days.</summary>
    public const string Week = "week";

    /// <summary>The next thirty-one days.</summary>
    public const string Month = "month";

    public static readonly IReadOnlyList<string> Ranges = [Upcoming, Week, Month];

    public string Key => CoreBlocks.Calendar;

    public async Task<JsonNode> ResolveAsync(
        JsonNode? props,
        DataBlockContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!BlockProps.TryDepartment(props, "department", out var department))
        {
            return DataBlockScope.Nothing("items");
        }

        var now = clock.UtcNow;
        var query = database.CalendarEntries
            .AsNoTracking()
            .Where(entry => entry.StartsAtUtc >= now)
            .WithinPage(context);

        if (Until(now, BlockProps.Text(props, "range")) is { } until)
        {
            query = query.Where(entry => entry.StartsAtUtc < until);
        }

        if (department is { } owner)
        {
            query = query.Where(entry => entry.OwnerDepartment == owner);
        }

        // A block that names no kind asks for every kind. The kinds are free strings — the staff
        // writes them — so an unknown one simply matches nothing.
        var kinds = BlockProps.Entries(props, "kinds", "kind").Distinct(StringComparer.Ordinal).ToList();
        if (kinds.Count > 0)
        {
            query = query.Where(entry => kinds.Contains(entry.Kind));
        }

        var entries = await query
            .OrderBy(entry => entry.StartsAtUtc)
            .ThenBy(entry => entry.Id)
            .Take(DataBlockScope.LimitOf(props))
            .ToListAsync(cancellationToken);

        var items = new JsonArray();
        foreach (var entry in entries)
        {
            items.Add(new JsonObject
            {
                ["id"] = entry.Id,
                ["kind"] = entry.Kind,
                ["title"] = BlockProps.Translated(entry.Title),
                ["description"] = BlockProps.Translated(entry.Description),
                ["startsAt"] = BlockProps.Instant(entry.StartsAtUtc),
                ["endsAt"] = entry.EndsAtUtc is { } ends ? BlockProps.Instant(ends) : null,
                ["allDay"] = entry.AllDay,
                ["department"] = entry.OwnerDepartment.ToString(),
                ["url"] = string.IsNullOrWhiteSpace(entry.Url) ? null : entry.Url,
            });
        }

        return new JsonObject { ["items"] = items };
    }

    private static DateTime? Until(DateTime now, string? range) => range switch
    {
        Week => now.AddDays(7),
        Month => now.AddDays(31),
        _ => null,
    };
}

/// <summary>
/// Rows of <c>cms_contents</c> of one kind, listed. News and documents are two configurations of
/// this one provider for the same reason they are two <c>kind</c>s of one table and not two tables
/// (plan section 9.3): what differs between them is an order and three columns, and neither is a
/// reason for a second reader.
/// </summary>
public abstract class ContentListProvider(HubDbContext database) : IDataBlockProvider
{
    public abstract string Key { get; }

    protected abstract ContentKind Kind { get; }

    /// <summary>How this kind reads: newest first, or in the order somebody put them in.</summary>
    protected abstract IQueryable<ContentEntry> Order(IQueryable<ContentEntry> query, JsonNode? props);

    /// <summary>The three columns that belong to this kind and not to the other.</summary>
    protected abstract void Describe(ContentEntry entry, JsonObject item);

    public async Task<JsonNode> ResolveAsync(
        JsonNode? props,
        DataBlockContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var kind = Kind;

        if (!BlockProps.TryDepartment(props, "department", out var department))
        {
            return DataBlockScope.Nothing("items");
        }


        // Published and visible are both already in the query filter; a template is never a page
        // and never appears in a list of them.
        var query = database.Contents
            .AsNoTracking()
            .Where(content => content.Kind == kind && !content.IsTemplate)
            .WithinPage(context);

        if (department is { } owner)
        {
            query = query.Where(content => content.OwnerDepartment == owner);
        }

        if (BlockProps.Text(props, "category") is { } category)
        {
            query = query.Where(content => content.Category == category);
        }

        var rows = await Order(query, props)
            .Take(DataBlockScope.LimitOf(props))
            .ToListAsync(cancellationToken);

        var items = new JsonArray();
        foreach (var row in rows)
        {
            var item = new JsonObject
            {
                ["id"] = row.Id,
                ["title"] = BlockProps.Translated(row.Title),
                ["summary"] = BlockProps.Translated(row.Summary),

                // The one place that decides where a row lives is the row itself, so a list and
                // the search index cannot point at two different addresses.
                ["url"] = row.Url,
                ["category"] = row.Category,
                ["department"] = row.OwnerDepartment.ToString(),
                ["publishedAt"] = row.PublishedAt is { } published ? BlockProps.Instant(published) : null,
            };

            Describe(row, item);
            items.Add(item);
        }

        return new JsonObject
        {
            ["items"] = items,
            ["categories"] = await VocabularyAsync(kind, department, cancellationToken),
        };
    }

    /// <summary>
    /// The words this kind is filed under, so that a list can show a shelf by its translated name
    /// and a public page can offer them as a filter (design M1 section 3.4).
    /// <para>It travels with the rows rather than from an address of its own, because there is no
    /// second question here: whoever may read this list may read the names of its shelves. A row
    /// whose category has since been deleted keeps its key and simply finds no name here, which is
    /// what "the list shows it as it is" means.</para>
    /// <para>Not narrowed by the page's visibility, unlike the rows: a category has no visibility
    /// of its own — it is a word, not content — and <see cref="ContentCategory"/> deliberately does
    /// not implement <c>IVisible</c>.</para>
    /// </summary>
    private async Task<JsonArray> VocabularyAsync(
        ContentKind kind,
        Department? department,
        CancellationToken cancellationToken)
    {
        var query = database.ContentCategories
            .AsNoTracking()
            .Where(category => category.Kind == kind && category.IsActive);

        if (department is { } owner)
        {
            query = query.Where(category => category.OwnerDepartment == owner);
        }

        var rows = await query
            .OrderBy(category => category.Sort)
            .ThenBy(category => category.Key)
            .ToListAsync(cancellationToken);

        var vocabulary = new JsonArray();

        // A block that names no department gathers the words of all of them, and two departments
        // may well have filed under the same key: one entry per key, the first in order winning.
        foreach (var category in rows.DistinctBy(category => category.Key, StringComparer.Ordinal))
        {
            vocabulary.Add(new JsonObject
            {
                ["key"] = category.Key,
                ["label"] = BlockProps.Translated(category.Label),
            });
        }

        return vocabulary;
    }
}

/// <summary>News, newest first, with the pinned ones on top when the block asks for it.</summary>
public sealed class NewsListProvider(HubDbContext database) : ContentListProvider(database)
{
    public override string Key => CoreBlocks.NewsList;

    protected override ContentKind Kind => ContentKind.News;

    protected override IQueryable<ContentEntry> Order(IQueryable<ContentEntry> query, JsonNode? props)
    {
        var newest = BlockProps.Flag(props, "pinnedFirst", fallback: true)
            ? query.OrderByDescending(content => content.Pinned).ThenByDescending(content => content.PublishedAt)
            : query.OrderByDescending(content => content.PublishedAt);

        return newest.ThenByDescending(content => content.Id);
    }

    protected override void Describe(ContentEntry entry, JsonObject item)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(item);

        item["coverMediaId"] = entry.CoverMediaId;
        item["pinned"] = entry.Pinned;
    }
}

/// <summary>Documents, in the order somebody put them in, with the file when there is one.</summary>
public sealed class DocumentListProvider(HubDbContext database) : ContentListProvider(database)
{
    public override string Key => CoreBlocks.DocumentList;

    protected override ContentKind Kind => ContentKind.Document;

    protected override IQueryable<ContentEntry> Order(IQueryable<ContentEntry> query, JsonNode? props) =>
        query
            .OrderBy(content => content.Category)
            .ThenBy(content => content.Sort)
            .ThenBy(content => content.Id);

    protected override void Describe(ContentEntry entry, JsonObject item)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(item);

        // A document with a file is a card with a download; one without is read in the browser
        // like any other page (design M1 section 3).
        item["fileMediaId"] = entry.FileMediaId;
        item["sort"] = entry.Sort;
    }
}

/// <summary>
/// The staff of the division, which is <b>whoever has signed in at least once</b>: there is no
/// endpoint anywhere that lists the roster of a division, and that is the data there is rather
/// than a limitation to work around (plan section 16.13). A page says so in a line of its own
/// instead of pretending to be complete.
/// <para>Name and position, and nothing else: no address, no account elsewhere, no figures. There
/// is no public member profile in this hub and there will not be one (plan section 9.7).</para>
/// </summary>
public sealed class StaffListProvider(HubDbContext database) : IDataBlockProvider
{
    public string Key => CoreBlocks.StaffList;

    public async Task<JsonNode> ResolveAsync(
        JsonNode? props,
        DataBlockContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!BlockProps.TryDepartment(props, "department", out var department))
        {
            return DataBlockScope.Nothing("groups");
        }

        var withFirs = BlockProps.Flag(props, "includeFirStaff", fallback: true);

        // A position of headquarters belongs to no division and gets no department and no FIR, so
        // it is left out by the same condition that keeps the two kinds that do belong.
        var query = database.UserStaffPositions
            .AsNoTracking()
            .Where(position => position.Department != null || position.Fir != null);

        if (department is { } owner)
        {
            query = query.Where(position => position.Department == owner);
        }
        else if (!withFirs)
        {
            query = query.Where(position => position.Department != null);
        }

        var positions = await query
            .Join(
                database.Users.AsNoTracking(),
                position => position.Vid,
                user => user.Vid,
                (position, user) => new { position.Department, position.Level, position.Fir, position.Position, user })
            .ToListAsync(cancellationToken);

        // Grouped here rather than in the browser because the order is the one thing about a
        // roster that is not a matter of taste: a department, then seniority, then a name.
        var groups = new JsonArray();

        foreach (var byDepartment in Enum.GetValues<Department>())
        {
            var members = positions.Where(row => row.Department == byDepartment).ToList();
            if (members.Count > 0)
            {
                groups.Add(Group(byDepartment.ToString(), fir: null, members.Select(row =>
                    (row.user, row.Level, row.Position))));
            }
        }

        if (department is null && withFirs)
        {
            var firs = positions
                .Where(row => row.Department is null && row.Fir is not null)
                .GroupBy(row => row.Fir!, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.Ordinal);

            foreach (var fir in firs)
            {
                groups.Add(Group(department: null, fir: fir.Key, fir.Select(row =>
                    (row.user, row.Level, row.Position))));
            }
        }

        return new JsonObject { ["groups"] = groups };
    }

    private static JsonObject Group(
        string? department,
        string? fir,
        IEnumerable<(HubUser User, StaffLevel? Level, string Position)> members)
    {
        var people = new JsonArray();

        var ordered = members
            .OrderBy(member => member.Level ?? StaffLevel.Member)
            .ThenBy(member => member.User.LastName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(member => member.User.FirstName, StringComparer.CurrentCultureIgnoreCase);

        foreach (var member in ordered)
        {
            people.Add(new JsonObject
            {
                ["vid"] = member.User.Vid,
                ["name"] = $"{member.User.FirstName} {member.User.LastName}".Trim(),
                ["position"] = member.Position,
                ["level"] = member.Level?.ToString(),
            });
        }

        return new JsonObject
        {
            ["department"] = department,
            ["fir"] = fir,
            ["members"] = people,
        };
    }
}
