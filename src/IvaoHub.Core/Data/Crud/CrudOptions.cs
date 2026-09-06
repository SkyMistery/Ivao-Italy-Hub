using System.Collections;
using System.Linq.Expressions;
using FluentValidation;
using IvaoHub.Core.Localization;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Data.Crud;

/// <summary>
/// Everything the CRUD engine needs to know about one entity. A back office resource costs this
/// object and nothing else: no controller, no repository, no hand written paging (plan section 16.6).
/// </summary>
/// <typeparam name="TEntity">The entity. When it is <see cref="Division.IOwnedByDepartment"/> the
/// engine runs in departmental mode; otherwise in global mode.</typeparam>
/// <typeparam name="TListDto">One row of the list.</typeparam>
/// <typeparam name="TDetailDto">One row in full, as the form loads it.</typeparam>
/// <typeparam name="TWriteDto">What a client may send.</typeparam>
public sealed class CrudOptions<TEntity, TListDto, TDetailDto, TWriteDto>
    where TEntity : class
{
    /// <summary>
    /// The permission area, for example <c>Links</c>: it gives <c>Links.View</c> for reads and
    /// <c>Links.Edit</c> for writes unless a policy is set explicitly.
    /// </summary>
    public string PermissionArea { get; set; } = string.Empty;

    /// <summary>
    /// What this resource is called in the contract: the operation names the generated client picks
    /// up, and the tag its calls are grouped under. It defaults to <see cref="PermissionArea"/>,
    /// which is right until two resources share one area — the category vocabulary is the first,
    /// because naming the shelves of a department is part of writing its content and not a
    /// permission anybody hands out separately (design M1 section 10.1).
    /// <para>Without it both resources would answer to <c>ContentList</c> and the client generator
    /// would keep whichever it read last, which is a resource quietly disappearing from the client
    /// rather than an error.</para>
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Overrides <c>{PermissionArea}.View</c>, for a resource with no department.</summary>
    public string? ReadPolicy { get; set; }

    /// <summary>Overrides <c>{PermissionArea}.Edit</c>, for a resource with no department.</summary>
    public string? WritePolicy { get; set; }

    /// <summary>Only the two reads are mapped. The audit log is the reason this exists.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Set to false for a resource that may be created and changed but never deleted.</summary>
    public bool AllowDelete { get; set; } = true;

    /// <summary>
    /// Set to false for a resource whose rows cannot be brought into existence by a JSON payload.
    /// The media library is the reason it exists: an upload is a multipart request, a row without
    /// a file on disk must not be able to exist, and moving the upload to a second address would
    /// have been a second way of creating a media (implementation plan M1, G1).
    /// <para>It is deliberately a property of the engine and not a branch about one entity: what a
    /// resource says here is "the create of this resource is not a JSON create", which is a thing
    /// any resource may be.</para>
    /// </summary>
    public bool MapCreate { get; set; } = true;

    /// <summary>
    /// What deleting a row of this resource means, when it does not mean removing it. The engine
    /// calls this instead of <c>Remove</c> and saves afterwards, so the audit row, the guard and
    /// the projections are the ones of an ordinary write.
    /// <para>The media library is the first: its rows outlive the deletion because an already
    /// published page names the identifier, and it is the <b>file</b> that goes — and only when
    /// nothing published still shows it.</para>
    /// </summary>
    public Func<TEntity, IServiceProvider, CancellationToken, Task>? Delete { get; set; }

    /// <summary>
    /// Which context owns the entity. The core answers for its own; a module points this at its
    /// own context, which <c>AddModuleDbContext</c> registered with the same interceptor.
    /// </summary>
    public Type ContextType { get; set; } = typeof(HubDbContext);

    /// <summary>How the list is ordered when the request does not say.</summary>
    public Expression<Func<TEntity, object?>>? DefaultOrder { get; set; }

    /// <summary>The columns <c>?q=</c> looks into, translated ones included.</summary>
    public CrudSearchFields<TEntity> SearchFields { get; } = [];

    /// <summary>Property names accepted in <c>?filter[name]=value</c>. Anything else is refused.</summary>
    public IList<string> Filterable { get; } = [];

    /// <summary>
    /// Filters that are not an equality on a column of the entity. <see cref="Filterable"/> covers
    /// the ordinary case and covers it in one line; this is for a question the row alone cannot
    /// answer — "which contents use this media?" reads a JSON body, not a column.
    /// <para>The function is handed the query and the raw value and returns the narrowed query, or
    /// null when the value makes no sense, which the engine answers with 400 exactly as it does
    /// for an unknown filter. The name lives in the same space as <see cref="Filterable"/>, so no
    /// resource can declare both.</para>
    /// </summary>
    public IDictionary<string, Func<IQueryable<TEntity>, string, IQueryable<TEntity>?>> CustomFilters { get; } =
        new Dictionary<string, Func<IQueryable<TEntity>, string, IQueryable<TEntity>?>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Property names accepted in <c>?sort=</c>. Anything else is refused.</summary>
    public IList<string> Sortable { get; } = [];

    /// <summary>
    /// A filter the list applies unless the caller names that property itself. It is how a
    /// resource says what its list is normally about -- the content list hides the templates, and
    /// shows them for <c>filter[isTemplate]=true</c> -- without a second query parameter and
    /// without the engine learning anything about the entity.
    /// <para>Every name here has to be in <see cref="Filterable"/> too: the default and the
    /// override are the same filter, so they are checked the same way.</para>
    /// </summary>
    public IDictionary<string, string> DefaultFilters { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Entity to list row. Generated by Mapperly, never written by hand.</summary>
    public Func<TEntity, TListDto>? ToList { get; set; }

    /// <summary>Entity to detail row.</summary>
    public Func<TEntity, TDetailDto>? ToDetail { get; set; }

    /// <summary>Write payload onto the entity, for both create and update.</summary>
    public Action<TWriteDto, TEntity>? Apply { get; set; }

    /// <summary>Taken from the container when it is not set here.</summary>
    public IValidator<TWriteDto>? Validator { get; set; }

    /// <summary>
    /// The rows the back office may reach. The default is every row of the set with the visibility
    /// filter switched off, because the staff has to see drafts and rows of other departments in
    /// order to be told they may not touch them; the department filter and the policies are what
    /// actually decide, right after (design M0 section 3.5).
    /// </summary>
    public Func<DbContext, IQueryable<TEntity>>? Source { get; set; }

    /// <summary>
    /// Which rows of this resource every department may read, whoever owns them. The engine puts it
    /// in <c>OR</c> with the department filter of the list, and only there: reading is the only
    /// thing it widens.
    /// <para>Templates are the reason it exists (design M1 section 9.4). The engine still does not
    /// know what a template is — what it is told is that this resource has some rows it shares —
    /// and the same expression is what <see cref="Division.ISharedForReading"/> answers with in
    /// memory, so the two sides cannot drift.</para>
    /// </summary>
    public Expression<Func<TEntity, bool>>? SharedForReading { get; set; }

    /// <summary>
    /// Which rows of this resource nobody may write, whatever they hold. It is not a permission and
    /// there is no policy that lifts it: these rows are written by something else, and a hand made
    /// change to one would be undone the next time that something else saves.
    /// <para>The calendar is the first: an entry projected from a module's own row is a mirror, and
    /// editing a mirror is watching the change come back (design M1 section 4). The engine still
    /// does not know what a projection is — what it is told is that this resource has rows it does
    /// not own — and the entity is the one place that decides which ones, so the badge the list
    /// draws and the refusal the engine gives cannot disagree.</para>
    /// <para>They are not hidden: the staff has to see them, and being told why they are read only
    /// is the whole difference between a disabled button and a ticket.</para>
    /// </summary>
    public Func<TEntity, bool>? ReadOnlyRows { get; set; }

    /// <summary>
    /// One extra policy a write on this particular row needs, on top of the write policy. The only
    /// extension point of the engine: it exists so that "editing a template needs
    /// <c>Content.ManageTemplates</c>" is configuration and not a special case in the endpoint
    /// (design M0 section 5.7). Returning null means no extra policy.
    /// </summary>
    public Func<TEntity, string?>? ExtraWritePolicy { get; set; }

    internal string EffectiveName =>
        string.IsNullOrWhiteSpace(Name) ? PermissionArea : Name;

    internal string EffectiveReadPolicy =>
        ReadPolicy ?? $"{PermissionArea}.View";

    internal string EffectiveWritePolicy =>
        WritePolicy ?? $"{PermissionArea}.Edit";
}

/// <summary>
/// The columns a free text search looks into. A plain column and a translated one are added the
/// same way; the engine knows which is which because the types differ.
/// </summary>
public sealed class CrudSearchFields<TEntity> : IEnumerable<CrudSearchField<TEntity>>
{
    private readonly List<CrudSearchField<TEntity>> _fields = [];

    /// <summary>A plain text column.</summary>
    public void Add(Expression<Func<TEntity, string?>> field)
    {
        ArgumentNullException.ThrowIfNull(field);
        _fields.Add(new CrudSearchField<TEntity>(_ => field, IsLocalized: false));
    }

    /// <summary>A translated column: searched in the language the reader is using.</summary>
    public void Add(Expression<Func<TEntity, Localized<string>>> field)
    {
        ArgumentNullException.ThrowIfNull(field);
        _fields.Add(new CrudSearchField<TEntity>(
            path => LocalizedQuery.Selector(field, path),
            IsLocalized: true));
    }

    public IEnumerator<CrudSearchField<TEntity>> GetEnumerator() => _fields.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>One searchable column, as the expression the engine puts in the <c>WHERE</c>.</summary>
/// <param name="Selector">Given the JSON path of the current language, the text to compare.</param>
/// <param name="IsLocalized">Whether the column is a translated one.</param>
public sealed record CrudSearchField<TEntity>(
    Func<string, Expression<Func<TEntity, string?>>> Selector,
    bool IsLocalized);
