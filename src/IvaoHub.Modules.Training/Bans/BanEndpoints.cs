using IvaoHub.Core.Auth;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Staff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Training.Bans;

/// <summary>
/// The bans in the back office (design M3 §2.9, §4.2): the generic list and form at <c>/api/training/bans</c>, read with
/// <c>Training.View</c> — the trainee's path shows them to whoever does training — and written with <c>Training.Ban</c>, which the one
/// handler denies to whoever the ban is about. A ban is given and never changed through the form, never deleted: «lift the ban» is a
/// verb of its own, which records who and when (<see cref="TrainingBans"/>). <c>filter[vid]</c> narrows the list to one member.
/// </summary>
public static class BanEndpoints
{
    public const string Pattern = "/api/training/bans";

    public static IEndpointRouteBuilder MapBanEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var clock = app.ServiceProvider.GetRequiredService<IClock>();
        var noNames = new Dictionary<int, string>();

        // Newest first unless the reader sorts, as the page asks it.
        app.MapCrud<TraineeBan, TraineeBanDto, TraineeBanDto, TraineeBanWriteDto>(Pattern, options =>
        {
            options.PermissionArea = TrainingPermissions.Area;
            options.Name = "TrainingBans";
            options.ReadPolicy = TrainingPermissions.View;
            options.WritePolicy = TrainingPermissions.Ban;
            options.ContextType = typeof(TrainingDbContext);
            options.AllowDelete = false;

            // A ban given is not changed through the form: it holds until its end, or until somebody lifts it with the verb below,
            // which records who and when (§2.9). Only a new one is written here.
            options.ReadOnlyRows = ban => ban.Id != 0;

            options.DefaultOrder = ban => ban.CreatedAt;
            options.Sortable.Add(nameof(TraineeBan.CreatedAt));
            options.Sortable.Add(nameof(TraineeBan.EndsAt));
            options.Filterable.Add(nameof(TraineeBan.Vid));
            options.SearchFields.Add(ban => ban.Reason);
            options.SearchFields.Add(ban => ban.Vid.ToString());

            options.ToList = ban => TrainingBans.Row(ban, noNames, clock.UtcNow);
            options.ToListPage = (bans, services, cancellationToken) =>
                services.GetRequiredService<TrainingBans>().RowsAsync(bans, cancellationToken);
            options.ToDetail = ban => TrainingBans.Row(ban, noNames, clock.UtcNow);
            options.Apply = (payload, ban) =>
            {
                ban.Vid = payload.Vid;
                ban.Reason = payload.Reason?.Trim() ?? string.Empty;
                ban.EndsAt = payload.EndsAt;
            };
            options.BeforeSave = (ban, saving) =>
                saving.Services.GetRequiredService<TrainingBans>().RefusalAsync(ban, saving.CancellationToken);

            // The member is told once, when the ban is given.
            options.AfterSave = (ban, saving) =>
                saving.IsNew ? saving.Services.GetRequiredService<TrainingBans>().TellAsync(ban, saving.CancellationToken) : Task.CompletedTask;
        });

        app.MapPost($"{Pattern}/{{id:long}}/lift", LiftAsync)
            .WithTags("TrainingBans")
            .WithName("TrainingBanLift")
            .RequireAuthorization(TrainingPermissions.Ban)
            .Produces<TraineeBanDto>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>«Lift the ban»: the ban as it is afterwards, or why not; 409 when it moved since the reader read it.</summary>
    private static async Task<IResult> LiftAsync(
        long id,
        TraineeBanLiftDto body,
        TrainingBans bans,
        LocaleCatalog catalog,
        ICurrentUser currentUser,
        HttpContext http)
    {
        var ban = await bans.FindAsync(id, tracked: true, http.RequestAborted);
        if (ban is null)
        {
            return Results.NotFound();
        }

        try
        {
            var (result, problems) = await bans.LiftAsync(ban, body.RowVersion, http.RequestAborted);
            return result switch
            {
                StaffResult.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                StaffResult.Refused => CrudProblems.Validation(problems!, new Dictionary<string, string[]>(), catalog, currentUser.Locale),
                _ => Results.Ok((await bans.RowsAsync([ban], http.RequestAborted))[0]),
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody else lifted it, or it moved, since the page was read: the reader reads it again.
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: catalog.Resolve(currentUser.Locale, CrudProblems.ConflictTitleKey));
        }
    }
}
