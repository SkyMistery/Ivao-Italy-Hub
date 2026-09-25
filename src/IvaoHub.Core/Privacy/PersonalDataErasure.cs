using System.Globalization;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Privacy;

/// <summary>What an erasure would do, for the superadmin to read before confirming.</summary>
/// <param name="Vid">The VID asked about.</param>
/// <param name="Name">Their name, when they have ever signed in: so the superadmin sees whom they are about to erase.</param>
/// <param name="IsSuperadmin">A super administrator is not erased: the role goes first.</param>
/// <param name="Lines">The core's lines, then each module's.</param>
public sealed record ErasurePreviewDto(int Vid, string? Name, bool IsSuperadmin, IReadOnlyList<ErasureLine> Lines);

/// <summary>What an erasure did.</summary>
/// <param name="Pseudonym">The number the person is now, in what stayed.</param>
/// <param name="Lines">The modules' lines, then the core's.</param>
public sealed record ErasureResultDto(int Pseudonym, IReadOnlyList<ErasureLine> Lines);

/// <summary>
/// Erases the data of one person (GDPR, art. 17): note <c>2026-09-25-la-cancellazione-dei-dati-di-una-persona</c>. Only a
/// super administrator asks for it (the endpoint checks), and it cannot be undone.
/// <para>In order, each in a transaction of its own and in erasure mode (<see cref="HubSaveChangesInterceptor.BeginErasure"/>):</para>
/// <list type="number">
/// <item>a pseudonym: a negative number, new for this erasure, that nothing ties back to the VID;</item>
/// <item>every enabled module: its <see cref="IPersonalDataEraser"/>, if it has one, deletes or empties its rows about the
/// person; then the core writes the pseudonym into every column of its contexts that names them (<see cref="PersonColumns"/>)
/// and empties the audit rows of what was erased;</item>
/// <item>the core the same way: the threads the person opened go whole, they leave the threads they took part in, and
/// their notifications, preferences, awards, tokens, grants, positions and user go;</item>
/// <item>the audit log (<see cref="AuditRedaction"/>), and one row that says an erasure happened — with the pseudonym, never
/// the VID.</item>
/// </list>
/// <para>Every step finds what is left by the VID, so an erasure that failed half way is simply run again. The person may
/// sign in again one day: IVAO gives the hub their name back, and they are a new member with no tie to the pseudonym.</para>
/// </summary>
public sealed class PersonalDataErasure(
    HubDbContext database,
    HubSaveChangesInterceptor interceptor,
    IEnumerable<IPersonalDataEraser> erasers,
    ModuleRegistry modules,
    IServiceProvider services,
    ISecurityStampCache stamps,
    ICurrentUser currentUser,
    IClock clock)
{
    /// <summary>Where the last pseudonym handed out is kept, in <c>hub_division_settings</c>.</summary>
    public const string PseudonymSettingKey = "privacy.lastPseudonym";

    /// <summary>The action of the one audit row an erasure leaves.</summary>
    public const string AuditAction = "erasure";

    public async Task<ErasurePreviewDto> PreviewAsync(int vid, CancellationToken cancellationToken = default)
    {
        Refuse(vid);

        var user = await database.Users.AsNoTracking()
            .Where(row => row.Vid == vid)
            .Select(row => new { row.FirstName, row.LastName, row.IsSuperadmin })
            .FirstOrDefaultAsync(cancellationToken);

        var threads = await CrudSource.BackOffice<ContactMessage>(database).CountAsync(row => row.CreatedBy == vid, cancellationToken);
        var lines = new List<ErasureLine>
        {
            new("erasure.lines.user", user is null ? 0 : 1, ErasureOutcome.Deleted),
            new("erasure.lines.access", await AccessRowsAsync(vid, cancellationToken), ErasureOutcome.Deleted),
            new("erasure.lines.notifications", (await NotificationsAboutAsync(vid, cancellationToken)).Count, ErasureOutcome.Deleted),
            new("erasure.lines.awards", await AwardRowsAsync(vid, cancellationToken), ErasureOutcome.Deleted),
            new("erasure.lines.threads", threads, ErasureOutcome.Deleted),
            new("erasure.lines.participation", await Participating(vid).CountAsync(cancellationToken), ErasureOutcome.Anonymised),
        };

        foreach (var eraser in EnabledErasers())
        {
            lines.AddRange(await eraser.PreviewAsync(vid, cancellationToken));
        }

        var name = user is null ? null : $"{user.FirstName} {user.LastName}".Trim();
        return new ErasurePreviewDto(vid, name, user?.IsSuperadmin == true, lines);
    }

    public async Task<ErasureResultDto> EraseAsync(int vid, CancellationToken cancellationToken = default)
    {
        Refuse(vid);

        if (await database.Users.AnyAsync(row => row.Vid == vid && row.IsSuperadmin, cancellationToken))
        {
            throw new DomainRefusalException("vid", "errors.erasure.superadmin");
        }

        var pseudonym = await NextPseudonymAsync(cancellationToken);
        var lines = new List<ErasureLine>();
        var named = 0;

        using (interceptor.BeginErasure())
        {
            foreach (var module in modules.Enabled)
            {
                var (moduleLines, moduleNamed) = await EraseModuleAsync(module, new ErasureRequest(vid, pseudonym), cancellationToken);
                lines.AddRange(moduleLines);
                named += moduleNamed;
            }

            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            lines.AddRange(await EraseCoreAsync(vid, cancellationToken));
            await database.SaveChangesAsync(cancellationToken);

            named += await PersonColumnRewrite.RewriteAsync(database, new ErasureRequest(vid, pseudonym), cancellationToken);
            await database.SaveChangesAsync(cancellationToken);

            await AuditRedaction.EmptyAsync(database, interceptor.TakeErased(database), cancellationToken);
            var audit = await AuditRedaction.ReplaceAsync(database, vid, pseudonym, cancellationToken);

            lines.Add(new ErasureLine("erasure.lines.named", named, ErasureOutcome.Anonymised));
            lines.Add(new ErasureLine("erasure.lines.audit", audit, ErasureOutcome.Anonymised));

            database.AuditLog.Add(new AuditLogEntry
            {
                Vid = currentUser.Vid,
                Action = AuditAction,
                Entity = AuditRedaction.UsersTable,
                EntityId = pseudonym.ToString(CultureInfo.InvariantCulture),
                AfterJson = JsonSerializer.Serialize(lines.Where(line => line.Count > 0)
                    .GroupBy(line => line.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Sum(line => line.Count))),
                IsSuperadmin = currentUser.IsSuperadmin,
                At = clock.UtcNow,
            });

            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        // Their cookie dies with their row; this only saves it the minute the cache would otherwise believe it.
        stamps.Invalidate(vid);
        return new ErasureResultDto(pseudonym, lines);
    }

    private static void Refuse(int vid)
    {
        if (vid <= 0)
        {
            throw new DomainRefusalException("vid", "errors.erasure.vid");
        }
    }

    private IEnumerable<IPersonalDataEraser> EnabledErasers() =>
        erasers.Where(eraser => modules.Find(eraser.ModuleKey) is not null);

    /// <summary>One module: its eraser, then the pseudonym and the audit in each of its contexts, all in one transaction per context.</summary>
    private async Task<(IReadOnlyList<ErasureLine> Lines, int Named)> EraseModuleAsync(
        IModule module,
        ErasureRequest request,
        CancellationToken cancellationToken)
    {
        var contexts = module.DbContextTypes
            .Select(type => services.GetService(type) as DbContext)
            .OfType<DbContext>()
            .ToList();

        var transactions = new List<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>();
        try
        {
            foreach (var context in contexts)
            {
                transactions.Add(await context.Database.BeginTransactionAsync(cancellationToken));
            }

            var eraser = erasers.FirstOrDefault(candidate => candidate.ModuleKey == module.Key);
            var lines = eraser is null ? [] : await eraser.EraseAsync(request, cancellationToken);

            var named = 0;
            foreach (var context in contexts)
            {
                named += await PersonColumnRewrite.RewriteAsync(context, request, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);

                await AuditRedaction.EmptyAsync(context, interceptor.TakeErased(context), cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }

            foreach (var transaction in transactions)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return (lines, named);
        }
        finally
        {
            foreach (var transaction in transactions)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <summary>The core's rows about the person, removed from the context; the caller saves.</summary>
    private async Task<IReadOnlyList<ErasureLine>> EraseCoreAsync(int vid, CancellationToken cancellationToken)
    {
        // The threads they opened, whole: what the staff answered quotes them (note §5, answer 3). The outcome of a dispute
        // stays on the row it was about, which is the module's.
        var threads = await CrudSource.BackOffice<ContactMessage>(database).AsTracking()
            .Where(row => row.CreatedBy == vid)
            .ToListAsync(cancellationToken);
        var ids = threads.Select(row => row.Id).ToArray();
        database.ContactReplies.RemoveRange(await database.ContactReplies.Where(row => ids.Contains(row.MessageId)).ToListAsync(cancellationToken));
        database.ContactReferences.RemoveRange(await database.ContactReferences.Where(row => ids.Contains(row.MessageId)).ToListAsync(cancellationToken));
        database.ContactMessages.RemoveRange(threads);

        // The threads of others they took part in: they leave the list, and their answers there take the pseudonym.
        var participating = await Participating(vid).AsTracking().ToListAsync(cancellationToken);
        foreach (var message in participating)
        {
            message.ParticipantsJson = ContactParticipants.Write(message.AddedParticipants, except: vid);
        }

        var notifications = await NotificationsAboutAsync(vid, cancellationToken);
        database.Notifications.RemoveRange(notifications);
        database.NotificationPreferences.RemoveRange(
            await database.NotificationPreferences.Where(row => row.Vid == vid).ToListAsync(cancellationToken));
        database.UserPreferences.RemoveRange(await database.UserPreferences.Where(row => row.Vid == vid).ToListAsync(cancellationToken));

        var assignments = await database.AwardAssignments.Where(row => row.Vid == vid).ToListAsync(cancellationToken);
        var signals = await database.AwardSignals.Where(row => row.Vid == vid).ToListAsync(cancellationToken);
        database.AwardAssignments.RemoveRange(assignments);
        database.AwardSignals.RemoveRange(signals);

        // What gave them access, removed one by one rather than left to the cascade: a row the database deletes on its own
        // never reaches the interceptor, and its copies in the audit log would stay.
        var access = 0;
        access += Remove(await database.PersonalTokens.Where(row => row.Vid == vid).ToListAsync(cancellationToken));
        access += Remove(await database.UserTokens.Where(row => row.Vid == vid).ToListAsync(cancellationToken));
        access += Remove(await database.UserGrants.Where(row => row.Vid == vid).ToListAsync(cancellationToken));
        access += Remove(await database.UserStaffPositions.Where(row => row.Vid == vid).ToListAsync(cancellationToken));
        var users = Remove(await database.Users.Where(row => row.Vid == vid).ToListAsync(cancellationToken));

        return
        [
            new("erasure.lines.user", users, ErasureOutcome.Deleted),
            new("erasure.lines.access", access, ErasureOutcome.Deleted),
            new("erasure.lines.notifications", notifications.Count, ErasureOutcome.Deleted),
            new("erasure.lines.awards", assignments.Count + signals.Count, ErasureOutcome.Deleted),
            new("erasure.lines.threads", threads.Count, ErasureOutcome.Deleted),
            new("erasure.lines.participation", participating.Count, ErasureOutcome.Anonymised),
        ];
    }

    private int Remove<TEntity>(List<TEntity> rows)
        where TEntity : class
    {
        database.Set<TEntity>().RemoveRange(rows);
        return rows.Count;
    }

    private IQueryable<ContactMessage> Participating(int vid)
    {
        var text = vid.ToString(CultureInfo.InvariantCulture);
        return CrudSource.BackOffice<ContactMessage>(database)
            .Where(row => row.CreatedBy != vid && JsonQuery.ContainsValue(row.ParticipantsJson, text));
    }

    /// <summary>
    /// The notifications to the person, and the ones about them: the department's mail about a thread they opened carries
    /// their VID, subject and text (<c>ContactThreads</c>), and a module's may too. Found by a LIKE and then read, because the
    /// data is JSON the core does not know the shape of.
    /// </summary>
    private async Task<List<Notification>> NotificationsAboutAsync(int vid, CancellationToken cancellationToken)
    {
        var pattern = $"%{vid.ToString(CultureInfo.InvariantCulture)}%";
        var rows = await database.Notifications
            .Where(row => row.Vid == vid || EF.Functions.Like(row.DataJson, pattern))
            .ToListAsync(cancellationToken);

        return [.. rows.Where(row => row.Vid == vid || AuditRedaction.Mentions(row.DataJson, vid))];
    }

    private async Task<int> AccessRowsAsync(int vid, CancellationToken cancellationToken) =>
        await database.PersonalTokens.CountAsync(row => row.Vid == vid, cancellationToken)
        + await database.UserTokens.CountAsync(row => row.Vid == vid, cancellationToken)
        + await database.UserGrants.CountAsync(row => row.Vid == vid, cancellationToken)
        + await database.UserStaffPositions.CountAsync(row => row.Vid == vid, cancellationToken);

    private async Task<int> AwardRowsAsync(int vid, CancellationToken cancellationToken) =>
        await database.AwardAssignments.CountAsync(row => row.Vid == vid, cancellationToken)
        + await database.AwardSignals.CountAsync(row => row.Vid == vid, cancellationToken);

    /// <summary>
    /// A new negative number, one below the last one handed out. Saved before the erasure starts and outside its mode, so the
    /// setting's own audit row is an ordinary one: it says a number was taken, and by whom, never for whom.
    /// </summary>
    private async Task<int> NextPseudonymAsync(CancellationToken cancellationToken)
    {
        var setting = await database.DivisionSettings.FirstOrDefaultAsync(row => row.Key == PseudonymSettingKey, cancellationToken);
        if (setting is null)
        {
            setting = new DivisionSetting { Key = PseudonymSettingKey, ValueJson = "0" };
            database.DivisionSettings.Add(setting);
        }

        var next = JsonSerializer.Deserialize<int>(setting.ValueJson) - 1;
        setting.ValueJson = JsonSerializer.Serialize(next);
        setting.UpdatedAt = clock.UtcNow;
        setting.UpdatedBy = currentUser.Vid;

        await database.SaveChangesAsync(cancellationToken);
        return next;
    }
}
