using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Privacy;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;

namespace IvaoHub.Core.Data;

/// <summary>
/// The only save changes interceptor of the hub. Everything that has to happen on every write
/// happens here, once, so that no module and no endpoint can forget it (design M0 section 3.4):
/// <list type="number">
/// <item>audit columns and timestamps of every <see cref="IAuditable"/>;</item>
/// <item>the write guard: nobody writes into the department of somebody else, not even by calling
/// <c>SaveChanges</c> directly with the policy forgotten;</item>
/// <item>a row in <c>hub_audit_log</c> for every entity marked <see cref="AuditedAttribute"/>;</item>
/// <item>the projections into search, calendar, award signals and uses of files, inside the very
/// transaction of the write;</item>
/// <item>a fresh <c>security_stamp</c> for every member whose session a written row decides
/// (<see cref="IAffectsUserSession"/>), so a permission that changed bites on the next request.</item>
/// </list>
/// <para>The last two run in a second pass, after the save: before it, a new row has no identifier
/// and there would be nothing to point an audit row or a projection at.</para>
/// </summary>
public sealed class HubSaveChangesInterceptor(
    ICurrentUser currentUser,
    IClock clock,
    ProjectionWriter projections,
    ProjectionContext projectionContext,
    IMemoryCache cache,
    IHttpContextAccessor? httpContext = null,
    ModuleRegistry? modules = null) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions AuditJson = BuildAuditJsonOptions();

    private static readonly ConcurrentDictionary<(Type Context, Type Entity), string> PermissionAreas = new();

    // Keyed by context rather than held as a field on it: the same scoped interceptor serves the
    // context of the core and the context of every module, and the state of one save must not be
    // visible to the other.
    private readonly Dictionary<DbContext, Pending> _pending = [];

    // Rows whose projection is out of date although nothing about them changed, waiting for the next
    // save of their context (ProjectionRefresh). Keyed by context for the same reason as above.
    private readonly Dictionary<DbContext, List<IProjectable>> _requested = [];

    // The erasure of a person's data (T20b): how many erasures are running in this scope, and the audited rows each context
    // deleted or emptied while one was, whose copies in the audit log have to go. See BeginErasure.
    private int _erasing;
    private readonly Dictionary<DbContext, List<(string Table, string Key)>> _erased = [];

    /// <summary>
    /// Puts every save of this scope in erasure mode until the result is disposed (note
    /// <c>2026-09-25-la-cancellazione-dei-dati-di-una-persona</c> §3). Only <c>PersonalDataErasure</c> calls it. In this mode:
    /// <list type="bullet">
    /// <item>nothing is stamped: a row that loses a person does not become "changed by the superadmin today", and its
    /// <c>created_by</c> may become the pseudonym, which <see cref="Stamp"/> otherwise forbids;</item>
    /// <item>an audited row deleted, or changed in anything but the columns that name people, is audited as <c>erased</c>
    /// <b>without</b> its data, and remembered (<see cref="TakeErased"/>) so that its earlier copies can be emptied too —
    /// otherwise erasing a row would write it into the audit log once more;</item>
    /// <item>a row changed only in the columns that name people is not audited at all: its history stays, and the erasure
    /// writes the pseudonym into it;</item>
    /// <item>no session is made stale: a grant whose author becomes the pseudonym has not changed for its holder, and the
    /// person being erased is signed out by losing their row.</item>
    /// </list>
    /// The guard and the projections are the same as ever.
    /// </summary>
    internal IDisposable BeginErasure()
    {
        _erasing++;
        return new ErasureMode(this);
    }

    /// <summary>The audited rows this context deleted or emptied in erasure mode since the last call, as table and key.</summary>
    internal List<(string Table, string Key)> TakeErased(DbContext context) =>
        _erased.Remove(context, out var rows) ? rows : [];

    private bool IsErasing => _erasing > 0;

    /// <summary>
    /// Asks the next save of this context to project these rows again, although none of them is being
    /// written. Only <see cref="ProjectionRefresh"/> calls it: see there for why it exists.
    /// </summary>
    internal void ProjectAgain(DbContext context, IEnumerable<IProjectable> rows)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rows);

        if (!_requested.TryGetValue(context, out var requested))
        {
            requested = [];
            _requested[context] = requested;
        }

        requested.AddRange(rows);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        var pending = Prepare(eventData.Context);
        if (pending is { NeedsTransaction: true, Context: { } context })
        {
            pending.OwnTransaction = context.Database.BeginTransaction();
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        var pending = Prepare(eventData.Context);
        if (pending is { NeedsTransaction: true, Context: { } context })
        {
            pending.OwnTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (TakeCompleted(eventData.Context) is not { } pending)
        {
            return base.SavedChanges(eventData, result);
        }

        try
        {
            pending.IsProjecting = true;
            WriteAuditRows(pending);
            ApplyProjections(pending);
            RefreshStaleSessions(pending);
            pending.Context.SaveChanges();
            pending.OwnTransaction?.Commit();
            ForgetStaleSessions(pending);
        }
        catch
        {
            // The second pass can throw on its own account: an entity whose Project() trips over
            // its own data, a projection row that violates a constraint. Without this the
            // transaction opened above would be left open and the entry left in _pending, and the
            // caller would see a failure whose write is neither committed nor rolled back.
            pending.OwnTransaction?.Rollback();
            throw;
        }
        finally
        {
            pending.IsProjecting = false;
            Release(pending);
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (TakeCompleted(eventData.Context) is not { } pending)
        {
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        try
        {
            pending.IsProjecting = true;
            WriteAuditRows(pending);
            await ApplyProjectionsAsync(pending, cancellationToken);
            await RefreshStaleSessionsAsync(pending, cancellationToken);
            await pending.Context.SaveChangesAsync(cancellationToken);

            if (pending.OwnTransaction is not null)
            {
                await pending.OwnTransaction.CommitAsync(cancellationToken);
            }

            ForgetStaleSessions(pending);
        }
        catch
        {
            // See the synchronous twin: a failure of the second pass must not leave the transaction
            // this interceptor opened hanging, nor the entry behind in _pending. The rollback is
            // not cancellable, because giving up on it is how a connection stays poisoned.
            if (pending.OwnTransaction is not null)
            {
                await pending.OwnTransaction.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        finally
        {
            pending.IsProjecting = false;
            Release(pending);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        // Not when the failure is the second pass itself: SavedChanges owns the cleanup there, and
        // rolling back and releasing here would leave it holding a transaction already disposed.
        if (TakeCompleted(eventData.Context) is { } pending)
        {
            pending.OwnTransaction?.Rollback();
            Release(pending);
        }

        base.SaveChangesFailed(eventData);
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        // Same as the synchronous twin: the second pass cleans up after itself.
        if (TakeCompleted(eventData.Context) is { } pending)
        {
            if (pending.OwnTransaction is not null)
            {
                await pending.OwnTransaction.RollbackAsync(CancellationToken.None);
            }

            Release(pending);
        }

        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <summary>
    /// First pass: stamps, guard, and what the second pass will need. Returns null when there is
    /// nothing to do afterwards, which is the common case of a write that is not audited and does
    /// not project.
    /// </summary>
    private Pending? Prepare(DbContext? context)
    {
        if (context is null || (_pending.TryGetValue(context, out var running) && running.IsProjecting))
        {
            // The second pass writes through the same context: audit rows and projections are the
            // result of the write, not a new write to be audited and projected again.
            return null;
        }

        var pending = new Pending(context);
        var vid = currentUser.IsAuthenticated ? currentUser.Vid : 0;
        var now = clock.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries().ToArray())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            // A write of bookkeeping only (a token's last use, T19a) is neither a change of the row nor something to audit.
            if (IsBookkeepingOnly(entry))
            {
                continue;
            }

            var erasing = IsErasing && entry.State is not EntityState.Added;
            if (!erasing)
            {
                Stamp(entry, vid, now);
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                ModuleBaseDepartment.Keep(modules, entry);
            }

            EnsureWriteIsAllowed(context, entry);

            if (erasing)
            {
                CollectErasure(context, entry, pending, vid, now);
            }
            else
            {
                CollectAudit(entry, pending, vid, now);
            }

            if (entry.Entity is IProjectable projectable)
            {
                // Until T4 a context without the projection tables skipped the projection here without
                // a word, and every row of a module went unprojected (note
                // 2026-09-15-contatti-con-risposte §3.3). Every context of the hub maps them now, so
                // one that does not is a mistake to be told about, not a case to be tolerated.
                if (!ProjectionWriter.CanProject(context))
                {
                    throw new InvalidOperationException(
                        $"{entry.Metadata.ClrType.Name} projects itself, but {context.GetType().Name} does not map the "
                        + "projection tables. A module context derives from ModuleDbContext, which maps them.");
                }

                pending.Projections.Add(new PendingProjection(projectable, entry.State == EntityState.Deleted));
            }

            if (!erasing && entry.Entity is IAffectsUserSession session && CanReachUsers(context))
            {
                CollectStaleSession(pending, session);

                // A row that changed its subject decides the session of whoever it was about before,
                // too: a grant moved from one member, or one position, to another.
                if (entry.State == EntityState.Modified && entry.OriginalValues.ToObject() is IAffectsUserSession before)
                {
                    CollectStaleSession(pending, before);
                }
            }
        }

        // Rows asked to be projected again without being written (ProjectionRefresh): projected the same
        // way, once, and only if the save did not already project them because they changed too.
        if (_requested.Remove(context, out var again))
        {
            foreach (var row in again.Where(row => !pending.Projections.Any(projection =>
                projection.Entity.SourceModule == row.SourceModule && projection.Entity.SourceId == row.SourceId)))
            {
                pending.Projections.Add(new PendingProjection(row, Removed: false));
            }
        }

        if (pending.Audits.Count == 0
            && pending.Projections.Count == 0
            && pending.StaleSessions.Count == 0
            && pending.StalePositions.Count == 0)
        {
            return null;
        }

        _pending[context] = pending;
        return pending;
    }

    private static bool IsBookkeepingOnly(EntityEntry entry) =>
        entry.State == EntityState.Modified
        && entry.Properties.Where(property => property.IsModified).All(property =>
            property.Metadata.PropertyInfo?.IsDefined(typeof(NotAuditedAttribute), inherit: false) == true);

    private static void Stamp(EntityEntry entry, int vid, DateTime now)
    {
        if (entry.Entity is not IAuditable auditable)
        {
            return;
        }

        switch (entry.State)
        {
            case EntityState.Added:
                auditable.CreatedAt = now;
                auditable.CreatedBy = vid;
                auditable.UpdatedAt = now;
                auditable.UpdatedBy = vid;
                break;

            case EntityState.Modified:
                auditable.UpdatedAt = now;
                auditable.UpdatedBy = vid;

                // Who created a row and when is written once and never rewritten, whatever the
                // caller put in the instance it handed over.
                entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// The safety net under the policies. An anonymous caller is the application itself (a job, a
    /// migration, the seed) and is left alone; a super administrator is allowed everything, which
    /// is the whole point of the role and is recorded in the audit row.
    /// </summary>
    private void EnsureWriteIsAllowed(DbContext context, EntityEntry entry)
    {
        if (!currentUser.IsAuthenticated || currentUser.IsSuperadmin || entry.Entity is not IOwnedByDepartment owned)
        {
            return;
        }

        // A row somebody sends to a department, rather than edits inside it. Creating one is open
        // to any member by design; everything after that — reading the queue, moving the status,
        // deleting — is an ordinary write and falls through to the permission below.
        if (entry is { State: EntityState.Added, Entity: ISubmittedByMembers })
        {
            return;
        }

        // The one exception after creation: a row somebody sent that is about them, which they keep changing — a pilot
        // withdraws or corrects their own report (M2, T11). Theirs before the write and after it, and in the same
        // departments: they may change it, not give it to somebody else or move it. Deleting is still the department's.
        if (entry is { State: EntityState.Modified, Entity: ISubmittedByMembers and IHasStakeholder { StakeholderVid: var after } }
            && after == currentUser.Vid
            && entry.OriginalValues.ToObject() is IHasStakeholder { StakeholderVid: var before }
            && before == currentUser.Vid
            && OriginalDepartments(entry).SequenceEqual(owned.OwnerDepartments))
        {
            return;
        }

        // Its twin for a row somebody takes part in (M2, T14): the sender or a participant of a thread answers it, and the
        // answer moves its status. A participant before the write and after it, and in the same departments.
        if (entry is { State: EntityState.Modified, Entity: ISubmittedByMembers and IHasParticipants { ParticipantVids: var now } }
            && now.Contains(currentUser.Vid)
            && entry.OriginalValues.ToObject() is IHasParticipants { ParticipantVids: var then }
            && then.Contains(currentUser.Vid)
            && OriginalDepartments(entry).SequenceEqual(owned.OwnerDepartments))
        {
            return;
        }

        // The second permission a row may be written with (M2, T13): a validator enabled on one tour decides its reports.
        // Asked with the row's scope, never of the member the row is about, and never to move the row somewhere else.
        if (entry.State == EntityState.Modified
            && entry.Metadata.ClrType.GetCustomAttributes(typeof(AlsoWrittenWithAttribute), inherit: false)
                is [AlsoWrittenWithAttribute alternative, ..]
            && (entry.Entity as IHasStakeholder)?.StakeholderVid != currentUser.Vid
            && OriginalDepartments(entry).SequenceEqual(owned.OwnerDepartments)
            && owned.OwnerDepartments.Any(department =>
                currentUser.Has(alternative.Permission, department, (entry.Entity as IHasResourceScope)?.ResourceScope)))
        {
            return;
        }

        var permission = ResolvePermissionArea(context, entry.Metadata.ClrType) + ".Edit";

        // Held on one of the departments of the row: whoever creates a row of a module has to put in
        // at least one department they hold the permission on (M2, note
        // 2026-09-13-moduli-non-subordinati-ai-dipartimenti §3.3). A row of one department has one.
        RequireAny(permission, owned.OwnerDepartments);

        if (entry.State == EntityState.Modified
            && OriginalDepartments(entry) is var original
            && !original.SequenceEqual(owned.OwnerDepartments))
        {
            // Moving a row between departments needs the permission on both sides, or it would be
            // a way of taking rows away from a department one row at a time.
            RequireAny(permission, original);
        }
    }

    /// <summary>The departments the row had before this write, read from the original values.</summary>
    private static IReadOnlyList<Department> OriginalDepartments(EntityEntry entry)
    {
        var owner = (Department)entry.Property(nameof(IOwnedByDepartment.OwnerDepartment)).OriginalValue!;
        var mask = DepartmentMask.IsStoredOn(entry.Metadata.ClrType)
            ? (int)entry.Property(DepartmentMask.PropertyName).OriginalValue! | DepartmentMask.Of(owner)
            : DepartmentMask.Of(owner);

        return DepartmentMask.Departments(mask);
    }

    private void RequireAny(string permission, IReadOnlyList<Department> departments)
    {
        if (departments.Any(department => currentUser.Has(permission, department)))
        {
            return;
        }

        throw new ForbiddenDomainException(
            $"VID {currentUser.Vid} does not hold {permission} on any of {string.Join(", ", departments)}.")
        {
            Permission = permission,
        };
    }

    /// <summary>
    /// The header the editor sends on a save it made by itself (G15; decision (B) of
    /// <c>decisions/2026-09-11-l-editor-che-risponde.md</c>). An update carrying it is audited as
    /// <c>autosaved</c>, with the names of the columns that moved and <b>without</b> their values:
    /// a draft stored at every pause would otherwise write its body twice into the audit log every
    /// ten seconds, on a database shared with vIPI. A save somebody pressed, a creation, a deletion
    /// and a publication are audited in full, as before — the published version is a row of its
    /// own and arrives here as <c>created</c>.
    /// </summary>
    public const string AutosaveHeader = "X-Hub-Autosave";

    private bool IsAutosave() =>
        httpContext?.HttpContext?.Request.Headers.TryGetValue(AutosaveHeader, out var value) == true
        && value == "1";

    private void CollectAudit(EntityEntry entry, Pending pending, int vid, DateTime now)
    {
        if (!entry.Metadata.ClrType.IsDefined(typeof(AuditedAttribute), inherit: false))
        {
            return;
        }

        var (action, before, after) = entry.State switch
        {
            EntityState.Added => ("created", null, Serialize(entry, current: true, changedOnly: false)),
            EntityState.Deleted => ("deleted", Serialize(entry, current: false, changedOnly: false), null),
            _ when IsAutosave() => ("autosaved", null, ChangedColumns(entry)),
            _ => ("updated", Serialize(entry, current: false, changedOnly: true), Serialize(entry, current: true, changedOnly: true)),
        };

        pending.Audits.Add(new PendingAudit(
            Entry: entry,
            Action: action,
            Table: entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
            // A deleted row loses its entry once the save is accepted, so its key is read now.
            Key: entry.State == EntityState.Deleted ? ReadKey(entry) : null,
            Before: before,
            After: after,
            Vid: vid,
            At: now));
    }

    /// <summary>
    /// The audit of a write in erasure mode (<see cref="BeginErasure"/>): a row deleted or emptied leaves a row that says so,
    /// with no data, and is remembered so that its earlier copies can be emptied; a row that only changed the people it
    /// names leaves nothing.
    /// </summary>
    private void CollectErasure(DbContext context, EntityEntry entry, Pending pending, int vid, DateTime now)
    {
        if (entry.State == EntityState.Modified
            && entry.Properties.Where(property => property.IsModified).All(property => PersonColumns.NamesPeople(property.Metadata)))
        {
            return;
        }

        if (!entry.Metadata.ClrType.IsDefined(typeof(AuditedAttribute), inherit: false))
        {
            return;
        }

        var table = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name;
        var key = ReadKey(entry);

        if (!_erased.TryGetValue(context, out var erased))
        {
            erased = [];
            _erased[context] = erased;
        }

        erased.Add((table, key));
        pending.Audits.Add(new PendingAudit(entry, "erased", table, key, Before: null, After: null, vid, now));
    }

    /// <summary>
    /// Whether this context can see <c>hub_users</c> at all. A module context has its own model
    /// and no user table in it, so there is nothing to stamp and nothing to fail about. (The
    /// projection tables used to be answered the same way; since T4 a module context maps them.)
    /// </summary>
    private static bool CanReachUsers(DbContext context) =>
        context.Model.FindEntityType(typeof(HubUser)) is not null;

    /// <summary>
    /// A new stamp for every member a written row decides the session of. It is set inside the same
    /// transaction as the write, so a rollback takes it back with everything else: a stamp that
    /// survived a failed grant would sign every one of that member's devices out for nothing.
    /// </summary>
    private static void CollectStaleSession(Pending pending, IAffectsUserSession session)
    {
        if (session.AffectedVid > 0)
        {
            pending.StaleSessions.Add(session.AffectedVid);
        }

        if (session.AffectedPosition is { } position)
        {
            pending.StalePositions.Add(position);
        }
    }

    /// <summary>The members who hold, now, a position a written row decides: their VIDs join the rest.</summary>
    private static void ResolveStalePositions(Pending pending)
    {
        foreach (var position in pending.StalePositions)
        {
            var levels = position.Levels.Cast<StaffLevel?>().ToArray();
            pending.StaleSessions.UnionWith(pending.Context.Set<UserStaffPosition>()
                .Where(held => held.Department == position.Department && levels.Contains(held.Level))
                .Select(held => held.Vid));
        }
    }

    private static async Task ResolveStalePositionsAsync(Pending pending, CancellationToken cancellationToken)
    {
        foreach (var position in pending.StalePositions)
        {
            var levels = position.Levels.Cast<StaffLevel?>().ToArray();
            pending.StaleSessions.UnionWith(await pending.Context.Set<UserStaffPosition>()
                .Where(held => held.Department == position.Department && levels.Contains(held.Level))
                .Select(held => held.Vid)
                .ToListAsync(cancellationToken));
        }
    }

    private static void RefreshStaleSessions(Pending pending)
    {
        ResolveStalePositions(pending);

        foreach (var user in LoadStaleUsers(pending))
        {
            user.SecurityStamp = SuperadminService.NewStamp();
        }
    }

    private static async Task RefreshStaleSessionsAsync(Pending pending, CancellationToken cancellationToken)
    {
        await ResolveStalePositionsAsync(pending, cancellationToken);

        if (pending.StaleSessions.Count == 0)
        {
            return;
        }

        var users = await pending.Context.Set<HubUser>()
            .Where(user => pending.StaleSessions.Contains(user.Vid))
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            user.SecurityStamp = SuperadminService.NewStamp();
        }
    }

    private static List<HubUser> LoadStaleUsers(Pending pending) =>
        pending.StaleSessions.Count == 0
            ? []
            : [.. pending.Context.Set<HubUser>().Where(user => pending.StaleSessions.Contains(user.Vid))];

    /// <summary>
    /// After the commit, and only after it: the cache holds what the database says, so dropping the
    /// entry before the write is durable would be inviting the next request to read the old row
    /// back in and cache it again.
    /// </summary>
    private void ForgetStaleSessions(Pending pending)
    {
        foreach (var vid in pending.StaleSessions)
        {
            SecurityStampCache.Forget(cache, vid);
        }
    }

    private void WriteAuditRows(Pending pending)
    {
        if (pending.Audits.Count == 0 || pending.Context.Model.FindEntityType(typeof(AuditLogEntry)) is null)
        {
            return;
        }

        // Only meaningful once the forwarded headers are trusted, which they are exactly as far as
        // the proxies the installation declares: see HubConfiguration.TrustedProxies.
        var ip = httpContext?.HttpContext?.Connection.RemoteIpAddress?.ToString();

        // A write a program made with a personal token says which token (T19a): revoking the right one needs it.
        var token = long.TryParse(
            httpContext?.HttpContext?.User.FindFirst(HubClaims.PersonalToken)?.Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var tokenId)
            ? tokenId
            : (long?)null;

        foreach (var audit in pending.Audits)
        {
            pending.Context.Set<AuditLogEntry>().Add(new AuditLogEntry
            {
                Vid = audit.Vid,
                Action = audit.Action,
                Entity = audit.Table,
                EntityId = audit.Key ?? ReadKey(audit.Entry),
                BeforeJson = audit.Before,
                AfterJson = audit.After,
                Ip = ip,
                TokenId = token,
                IsSuperadmin = currentUser.IsSuperadmin,
                At = audit.At,
            });
        }
    }

    private async Task ApplyProjectionsAsync(Pending pending, CancellationToken cancellationToken)
    {
        var requests = BuildRequests(pending);
        if (requests.Count == 0)
        {
            return;
        }

        var state = await ProjectionWriter.LoadAsync(pending.Context, requests, cancellationToken);
        projections.Apply(pending.Context, state, requests, projectionContext);
    }

    private void ApplyProjections(Pending pending)
    {
        var requests = BuildRequests(pending);
        if (requests.Count == 0)
        {
            return;
        }

        var state = ProjectionWriter.Load(pending.Context, requests);
        projections.Apply(pending.Context, state, requests, projectionContext);
    }

    /// <summary>
    /// What every touched row wants to look like, decided in one place before anything is read: the
    /// writer then loads what those sources have projected so far in one query per table for the
    /// whole save, instead of one per table for each row.
    /// </summary>
    private List<ProjectionRequest> BuildRequests(Pending pending) =>
    [
        .. pending.Projections.Select(projection => new ProjectionRequest(
            projection.Entity.SourceModule,
            projection.Entity.SourceId,
            Snapshot(projection))),
    ];

    private ProjectionSnapshot? Snapshot(PendingProjection projection)
    {
        if (projection.Removed)
        {
            return null;
        }

        var snapshot = projection.Entity.Project(projectionContext);

        // A draft has nothing public to find: the rule lives here, once, instead of in every entity
        // that can be published. What it keeps is the files it uses — a tour being prepared needs its
        // banner as much as an open one (note 2026-09-15-file-con-scadenza §3).
        return projection.Entity is IPublishable { Status: not PublishStatus.Published }
            ? snapshot?.Unpublished()
            : snapshot;
    }

    private Pending? TakeCompleted(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending) || pending.IsProjecting)
        {
            return null;
        }

        return pending;
    }

    private void Release(Pending pending)
    {
        pending.OwnTransaction?.Dispose();
        _pending.Remove(pending.Context);
    }

    private static string ReadKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return string.Empty;
        }

        var values = key.Properties.Select(property =>
            Convert.ToString(entry.Property(property.Name).CurrentValue, CultureInfo.InvariantCulture) ?? string.Empty);

        return string.Join(':', values);
    }

    private static string ResolvePermissionArea(DbContext context, Type entityType) =>
        PermissionAreas.GetOrAdd((context.GetType(), entityType), key =>
        {
            if (key.Entity.GetCustomAttributes(typeof(PermissionAreaAttribute), inherit: false)
                is [PermissionAreaAttribute declared, ..])
            {
                return declared.Area;
            }

            // Default: the name of the set the entity is exposed as, so Link becomes Links.Edit.
            var set = key.Context.GetProperties().FirstOrDefault(property =>
                property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>)
                && property.PropertyType.GetGenericArguments()[0] == key.Entity);

            return set?.Name ?? key.Entity.Name;
        });

    /// <summary>What an autosave is audited with: which columns moved, as a JSON array of names.</summary>
    private static string ChangedColumns(EntityEntry entry) =>
        JsonSerializer.Serialize(
            entry.Properties.Where(property => property.IsModified).Select(property => property.Metadata.Name).ToArray(),
            AuditJson);

    private static string Serialize(EntityEntry entry, bool current, bool changedOnly)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsConcurrencyToken || (changedOnly && !property.IsModified))
            {
                continue;
            }

            values[property.Metadata.Name] = current ? property.CurrentValue : property.OriginalValue;
        }

        return JsonSerializer.Serialize(values, AuditJson);
    }

    private static JsonSerializerOptions BuildAuditJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new LocalizedJsonConverterFactory());
        return options;
    }

    private sealed class Pending(DbContext context)
    {
        public DbContext Context { get; } = context;

        public bool IsProjecting { get; set; }

        public IDbContextTransaction? OwnTransaction { get; set; }

        public List<PendingAudit> Audits { get; } = [];

        public List<PendingProjection> Projections { get; } = [];

        /// <summary>VIDs whose cookie has to stop being believed once this write is committed.</summary>
        public HashSet<int> StaleSessions { get; } = [];

        /// <summary>Positions whose holders' cookies have to stop being believed, resolved into VIDs after the write.</summary>
        public HashSet<StaffPositionSubject> StalePositions { get; } = [];

        /// <summary>
        /// The second pass has to land in the same transaction as the write. When the caller opened
        /// one, it stays theirs to commit; otherwise this interceptor opens and closes its own.
        /// </summary>
        public bool NeedsTransaction => Context.Database.CurrentTransaction is null;
    }

    private sealed record PendingAudit(
        EntityEntry Entry,
        string Action,
        string Table,
        string? Key,
        string? Before,
        string? After,
        int Vid,
        DateTime At);

    private sealed record PendingProjection(IProjectable Entity, bool Removed);

    private sealed class ErasureMode(HubSaveChangesInterceptor interceptor) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                interceptor._erasing--;
            }
        }
    }
}
