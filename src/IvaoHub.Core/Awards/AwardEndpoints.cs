using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Core.Awards;

/// <summary>
/// The awards of the division, in three resources of the generic CRUD engine and not one line of a
/// hand written endpoint (M2, T4b; plan section 9.1):
/// <list type="bullet">
/// <item>the <b>catalogue</b>, <c>/api/awards</c>, departmental like the links — <c>Awards.View</c> and
/// <c>Awards.Edit</c> on the department of the award — and read by every department;</item>
/// <item>the <b>register</b>, <c>/api/award-assignments</c>, global behind <c>Awards.Assign</c>: an
/// assignment that answers a line of the queue marks that line handled in the same save;</item>
/// <item>the <b>queue</b>, <c>/api/award-signals</c>, global behind the same permission: the signals the
/// modules project, which a person dismisses or answers with an assignment.</item>
/// </list>
/// The hub never assigns an award by itself, and never shows a member their awards: the member sees
/// them on their IVAO profile (Carmine, 16 September 2026).
/// </summary>
public static class AwardEndpoints
{
    public const string AwardsPattern = "/api/awards";

    public const string AssignmentsPattern = "/api/award-assignments";

    public const string SignalsPattern = "/api/award-signals";

    public static IEndpointRouteBuilder MapAwardEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mapper = new AwardMapper();

        MapCatalogue(app, mapper);
        MapRegister(app, mapper);
        MapQueue(app, mapper);

        return app;
    }

    private static void MapCatalogue(IEndpointRouteBuilder app, AwardMapper mapper) =>
        app.MapCrud<Award, AwardListDto, AwardDetailDto, AwardWriteDto>(AwardsPattern, options =>
        {
            options.PermissionArea = CorePermissions.AwardsArea;

            options.DefaultOrder = award => award.Id;

            options.Sortable.Add(nameof(Award.UpdatedAt));
            options.Filterable.Add(nameof(Award.OwnerDepartment));
            options.Filterable.Add(nameof(Award.IsActive));
            options.SearchFields.Add(award => award.Name);

            // Whoever assigns reads every award, whichever department wrote it.
            options.SharedForReading = award => true;

            // Deleting is for an award nobody has received: one somebody holds is retired instead, so
            // the register keeps saying what was assigned.
            options.Delete = async (award, services, cancellationToken) =>
            {
                var database = services.GetRequiredService<HubDbContext>();

                if (await database.AwardAssignments.AnyAsync(row => row.AwardId == award.Id, cancellationToken))
                {
                    throw new DomainRefusalException("id", "errors.awards.assigned");
                }

                database.Awards.Remove(award);
            };

            options.ToList = mapper.ToList;
            options.ToDetail = mapper.ToDetail;
            options.Apply = mapper.Apply;
        });

    private static void MapRegister(IEndpointRouteBuilder app, AwardMapper mapper) =>
        app.MapCrud<AwardAssignment, AwardAssignmentListDto, AwardAssignmentDetailDto, AwardAssignmentWriteDto>(
            AssignmentsPattern,
            options =>
            {
                // No department: an assignment belongs to a member. The policy is the whole check, the
                // shape the grants set.
                options.PermissionArea = CorePermissions.AwardsArea;
                options.Name = "AwardAssignments";
                options.ReadPolicy = CorePermissions.AwardsAssign;
                options.WritePolicy = CorePermissions.AwardsAssign;

                // Newest first: the register is read from what was just decided.
                options.DefaultOrder = assignment => assignment.CreatedAt;

                options.Sortable.Add(nameof(AwardAssignment.Vid));
                options.Sortable.Add(nameof(AwardAssignment.CreatedAt));
                options.Filterable.Add(nameof(AwardAssignment.Vid));
                options.Filterable.Add(nameof(AwardAssignment.AwardId));
                options.SearchFields.Add(assignment => assignment.Reason);

                options.ToList = mapper.ToList;
                options.ToListPage = async (rows, services, cancellationToken) =>
                {
                    var names = await NamesOfAsync(services, rows.Select(row => (long?)row.AwardId), cancellationToken);
                    return [.. rows.Select(row => mapper.ToList(row) with { AwardName = names.GetValueOrDefault(row.AwardId) })];
                };
                options.ToDetail = mapper.ToDetail;
                options.Apply = mapper.Apply;
                options.BeforeSave = BeforeAssigningAsync;
            });

    private static void MapQueue(IEndpointRouteBuilder app, AwardMapper mapper) =>
        app.MapCrud<AwardSignal, AwardSignalListDto, AwardSignalDetailDto, AwardSignalWriteDto>(
            SignalsPattern,
            options =>
            {
                options.PermissionArea = CorePermissions.AwardsArea;
                options.Name = "AwardSignals";
                options.ReadPolicy = CorePermissions.AwardsAssign;
                options.WritePolicy = CorePermissions.AwardsAssign;

                // Oldest first: a queue is worked from whoever has waited longest.
                options.DefaultOrder = signal => signal.Id;

                options.Sortable.Add(nameof(AwardSignal.CreatedAt));
                options.Sortable.Add(nameof(AwardSignal.Vid));
                options.Filterable.Add(nameof(AwardSignal.Status));
                options.Filterable.Add(nameof(AwardSignal.SourceModule));
                options.Filterable.Add(nameof(AwardSignal.Vid));
                options.DefaultFilters[nameof(AwardSignal.Status)] = nameof(AwardSignalStatus.Pending);
                options.SearchFields.Add(signal => signal.Reason);

                // A signal is born from a row of a module and is never deleted: the queue is a record
                // of what was decided.
                options.MapCreate = false;
                options.AllowDelete = false;

                options.ToList = mapper.ToList;
                options.ToListPage = async (rows, services, cancellationToken) =>
                {
                    var names = await NamesOfAsync(services, rows.Select(row => row.AwardId), cancellationToken);
                    return [.. rows.Select(row => mapper.ToList(row) with
                    {
                        AwardName = row.AwardId is { } award ? names.GetValueOrDefault(award) : null,
                    })];
                };
                options.ToDetail = mapper.ToDetail;
                options.Apply = (payload, signal) => signal.Status = payload.Status;
                options.BeforeSave = BeforeMovingASignal;
            });

    /// <summary>
    /// The award has to exist, and be active when it is chosen; and the line of the queue an assignment
    /// answers has to be waiting and be about the same member — then it is handled, in this same save.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> BeforeAssigningAsync(
        AwardAssignment assignment,
        CrudSaving saving)
    {
        var database = saving.Services.GetRequiredService<HubDbContext>();
        var entry = saving.Database.Entry(assignment);

        if (!saving.IsNew)
        {
            // The line of the queue an assignment answered is a fact of its creation: an update keeps it.
            var signalProperty = entry.Property(row => row.SignalId);
            signalProperty.CurrentValue = signalProperty.OriginalValue;
            signalProperty.IsModified = false;
        }

        if (saving.IsNew || entry.Property(row => row.AwardId).IsModified)
        {
            var award = await database.Awards
                .AsNoTracking()
                .Where(row => row.Id == assignment.AwardId)
                .Select(row => new { row.IsActive })
                .FirstOrDefaultAsync(saving.CancellationToken);

            if (award is null || !award.IsActive)
            {
                return Refusal("awardId", award is null ? "errors.awards.unknown" : "errors.awards.retired");
            }
        }

        if (!saving.IsNew || assignment.SignalId is not { } signalId)
        {
            return null;
        }

        var signal = await database.AwardSignals.FirstOrDefaultAsync(row => row.Id == signalId, saving.CancellationToken);

        if (signal is null || signal.Status != AwardSignalStatus.Pending)
        {
            return Refusal("signalId", "errors.awards.signalNotPending");
        }

        if (signal.Vid != assignment.Vid)
        {
            return Refusal("vid", "errors.awards.signalOtherMember");
        }

        signal.Status = AwardSignalStatus.Handled;
        signal.HandledAt = saving.Services.GetRequiredService<IClock>().UtcNow;
        signal.HandledBy = saving.CurrentUser.Vid;

        return null;
    }

    /// <summary>A handled signal stays handled; a dismissed one records who dismissed it and when.</summary>
    private static Task<IReadOnlyDictionary<string, string[]>?> BeforeMovingASignal(AwardSignal signal, CrudSaving saving)
    {
        if (saving.Database.Entry(signal).Property(row => row.Status).OriginalValue == AwardSignalStatus.Handled)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string[]>?>(Refusal("status", "errors.awards.handledByAssignment"));
        }

        var dismissed = signal.Status == AwardSignalStatus.Dismissed;
        signal.HandledAt = dismissed ? saving.Services.GetRequiredService<IClock>().UtcNow : null;
        signal.HandledBy = dismissed ? saving.CurrentUser.Vid : null;

        return Task.FromResult<IReadOnlyDictionary<string, string[]>?>(null);
    }

    /// <summary>The names of the awards a page of rows names, in one query.</summary>
    private static async Task<Dictionary<long, Localized<string>>> NamesOfAsync(
        IServiceProvider services,
        IEnumerable<long?> awardIds,
        CancellationToken cancellationToken)
    {
        long[] ids = [.. awardIds.OfType<long>().Distinct()];
        if (ids.Length == 0)
        {
            return [];
        }

        return await services.GetRequiredService<HubDbContext>().Awards
            .AsNoTracking()
            .Where(award => ids.Contains(award.Id))
            .ToDictionaryAsync(award => award.Id, award => award.Name, cancellationToken);
    }

    private static IReadOnlyDictionary<string, string[]> Refusal(string field, string key) =>
        new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [key] };
}
