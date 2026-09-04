using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
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
/// <item>the projections into search, calendar and award signals, inside the very transaction of
/// the write;</item>
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
    IHttpContextAccessor? httpContext = null) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions AuditJson = BuildAuditJsonOptions();

    private static readonly ConcurrentDictionary<(Type Context, Type Entity), string> PermissionAreas = new();

    // Keyed by context rather than held as a field on it: the same scoped interceptor serves the
    // context of the core and the context of every module, and the state of one save must not be
    // visible to the other.
    private readonly Dictionary<DbContext, Pending> _pending = [];

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

            Stamp(entry, vid, now);
            EnsureWriteIsAllowed(context, entry);
            CollectAudit(entry, pending, vid, now);

            if (entry.Entity is IProjectable projectable && ProjectionWriter.CanProject(context))
            {
                pending.Projections.Add(new PendingProjection(projectable, entry.State == EntityState.Deleted));
            }

            if (entry.Entity is IAffectsUserSession { AffectedVid: > 0 } session && CanReachUsers(context))
            {
                pending.StaleSessions.Add(session.AffectedVid);
            }
        }

        if (pending.Audits.Count == 0 && pending.Projections.Count == 0 && pending.StaleSessions.Count == 0)
        {
            return null;
        }

        _pending[context] = pending;
        return pending;
    }

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

        var permission = ResolvePermissionArea(context, entry.Metadata.ClrType) + ".Edit";
        Require(permission, owned.OwnerDepartment);

        if (entry.State == EntityState.Modified
            && entry.Property(nameof(IOwnedByDepartment.OwnerDepartment)).OriginalValue is Department original
            && original != owned.OwnerDepartment)
        {
            // Moving a row between departments needs the permission on both sides, or it would be
            // a way of taking rows away from a department one row at a time.
            Require(permission, original);
        }
    }

    private void Require(string permission, Department department)
    {
        if (currentUser.Has(permission, department))
        {
            return;
        }

        throw new ForbiddenDomainException(
            $"VID {currentUser.Vid} does not hold {permission} on {department}.")
        {
            Permission = permission,
        };
    }

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
    /// Whether this context can see <c>hub_users</c> at all. A module context has its own model
    /// and no user table in it, so there is nothing to stamp and nothing to fail about; the same
    /// answer <c>ProjectionWriter.CanProject</c> gives about the projection tables.
    /// </summary>
    private static bool CanReachUsers(DbContext context) =>
        context.Model.FindEntityType(typeof(HubUser)) is not null;

    /// <summary>
    /// A new stamp for every member a written row decides the session of. It is set inside the same
    /// transaction as the write, so a rollback takes it back with everything else: a stamp that
    /// survived a failed grant would sign every one of that member's devices out for nothing.
    /// </summary>
    private static void RefreshStaleSessions(Pending pending)
    {
        foreach (var user in LoadStaleUsers(pending))
        {
            user.SecurityStamp = SuperadminService.NewStamp();
        }
    }

    private static async Task RefreshStaleSessionsAsync(Pending pending, CancellationToken cancellationToken)
    {
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
    /// writer then loads what those sources have projected so far in three queries for the whole
    /// save, instead of three for each row.
    /// </summary>
    private List<ProjectionRequest> BuildRequests(Pending pending) =>
    [
        .. pending.Projections.Select(projection => new ProjectionRequest(
            projection.Entity.SourceModule,
            projection.Entity.SourceId,
            // A draft has nothing public to find: the rule lives here, once, instead of in every
            // entity that can be published.
            projection.Removed || projection.Entity is IPublishable { Status: not PublishStatus.Published }
                ? null
                : projection.Entity.Project(projectionContext))),
    ];

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
}
