using IvaoHub.Core.Data.Crud;
using IvaoHub.Modules.Training.Bans;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Training.Staff;

/// <summary>
/// The path of a trainee as the staff reads it (design M3 §4.2, §2.8, §2.9), the page <c>/staff/training/trainees/{vid}</c>: who they
/// are; where they stand on each ladder — their rating and hours, what they may ask for next or why not, a mock exam agreed with a
/// trainer, «ready for the exam», the waiting, a ban —, the answer their own page gives (<see cref="MyTrainingPathDto"/>); every
/// training of theirs, open and closed, newest first, as the staff's page of each one reads it; their bans; and whether the reader may
/// ban them.
/// </summary>
/// <param name="Trainee">The member, by VID and by the name the hub has.</param>
/// <param name="Ladders">Where they stand on each ladder, in the order of the core's ladders.</param>
/// <param name="Trainings">
/// Their trainings, each as the staff's page of it (<c>StaffTrainings.PageAsync</c>): so a trainer who reads their own path reads it
/// without what is reserved, as on the page of each training (note <c>le-note-riservate-e-il-trainee</c>).
/// </param>
/// <param name="Bans">Their bans, the newest first: the ones that hold, the ones over and the ones lifted.</param>
/// <param name="CanBan">Whether the reader may ban them now: <c>Training.Ban</c>, never on themselves.</param>
public sealed record TraineePathDto(
    TrainingMemberDto Trainee,
    IReadOnlyList<MyTrainingPathDto> Ladders,
    IReadOnlyList<StaffTrainingDto> Trainings,
    IReadOnlyList<TraineeBanDto> Bans,
    bool CanBan);

/// <summary>
/// The one answer to «where is this trainee», for the staff (§4.2), read with <c>Training.View</c> — the staff of the training reads
/// every training (R.1, d2) —, like the page of the pilot of the tours (design M2 §8.7). It builds nothing of its own: the ladders are
/// the trainee's own answer (<see cref="TrainingRequests.PathsOfAsync"/>), each training the staff's page of it
/// (<see cref="StaffTrainings.PageAsync"/>, which leaves out what is reserved when the reader is its trainee), the bans their rows
/// (<see cref="TrainingBans"/>). A training the one handler does not let the reader read is left out.
/// </summary>
public sealed class TraineePaths(
    TrainingDbContext database,
    TrainingRequests requests,
    StaffTrainings staff,
    TrainingBans bans,
    TrainingPeople people)
{
    /// <summary>The path of <paramref name="vid"/>; none for a member the hub does not know and who has no training and no ban.</summary>
    public async Task<TraineePathDto?> ReadAsync(int vid, CancellationToken cancellationToken)
    {
        var history = await CrudSource.BackOffice<Training>(database).AsNoTracking()
            .Where(training => training.TraineeVid == vid)
            .OrderByDescending(training => training.CreatedAt)
            .ThenByDescending(training => training.Id)
            .ToListAsync(cancellationToken);
        var given = await bans.OfAsync(vid, cancellationToken);
        var names = await people.NamesAsync([vid], cancellationToken);

        if (history.Count == 0 && given.Count == 0 && !names.ContainsKey(vid))
        {
            return null;
        }

        var pages = new List<StaffTrainingDto>();
        foreach (var training in history)
        {
            if (await staff.MayAsync(training, TrainingPermissions.View))
            {
                pages.Add(await staff.PageAsync(training, cancellationToken));
            }
        }

        return new TraineePathDto(
            TrainingPeople.Member(vid, names)!,
            await requests.PathsOfAsync(vid, history, given, cancellationToken),
            pages,
            await bans.RowsAsync(given, cancellationToken),
            await bans.MayBanAsync(vid));
    }
}

public static class TraineePathEndpoints
{
    public const string Pattern = "/api/training/trainees";

    public static IEndpointRouteBuilder MapTraineePathEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{Pattern}/{{vid:int}}", async (int vid, TraineePaths paths, HttpContext http) =>
                await paths.ReadAsync(vid, http.RequestAborted) is { } path ? Results.Ok(path) : Results.NotFound())
            .WithTags("TrainingTrainees")
            .WithName("TrainingTraineePath")
            .RequireAuthorization(TrainingPermissions.View)
            .Produces<TraineePathDto>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
