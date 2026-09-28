using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.Training.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.Modules.Training.Exams;

/// <summary>
/// The exams in the back office (design M3 §1.5, §2.8, §4.2): the generic list and form at <c>/api/training/exams</c>, read with
/// <c>Training.View</c> — whoever does training sees the exams, which the calendar shows anyway — and written with
/// <c>Training.ManageExams</c>, which reaches only the exams assigned to the writer and, on any other, is worth <c>Training.Edit</c>
/// (note 2026-09-26-le-righe-affidate-a-chi-scrive §3.6). The engine asks the one handler on the row as it is stored and as it
/// would become, and before a delete, so the endpoint answers as the write guard does.
/// <para>No <c>DeletePolicy</c>, on purpose: an exam is taken off the calendar by the direction, the coordinator, the assistant and
/// the advisor it is assigned to, nobody else (Carmine, answer 4 on #131) — the write permission on the row says exactly that, and the
/// guard lets the examiner delete through <c>AlsoOnDeletion</c>. A <c>DeletePolicy</c> of <c>Training.Edit</c> would take their own
/// exam away from the advisor.</para>
/// <para>The list is newest first unless the reader sorts, as the page asks it; <c>filter[examinerVid]</c> narrows it to one examiner's —
/// the reader's own, «mine» —, <c>filter[kind]</c> to a ladder, and <c>?q=</c> looks into the position and the candidate's VID.</para>
/// </summary>
public static class ExamEndpoints
{
    public const string Pattern = "/api/training/exams";

    /// <summary>What the form chooses from: outside <see cref="Pattern"/>, whose second segment is an exam.</summary>
    public const string ChoicesPattern = "/api/training/exam-choices";

    public static IEndpointRouteBuilder MapExamEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var vocabulary = app.ServiceProvider.GetRequiredService<RatingVocabulary>();

        app.MapCrud<Exam, ExamRowDto, ExamDto, ExamWriteDto>(Pattern, options =>
        {
            options.PermissionArea = TrainingPermissions.Area;
            options.Name = "TrainingExams";
            options.ReadPolicy = TrainingPermissions.View;
            options.WritePolicy = TrainingPermissions.ManageExams;
            options.ContextType = typeof(TrainingDbContext);

            options.DefaultOrder = exam => exam.StartsAtUtc;
            options.Sortable.Add(nameof(Exam.StartsAtUtc));
            options.Filterable.Add(nameof(Exam.ExaminerVid));
            options.Filterable.Add(nameof(Exam.Kind));
            options.SearchFields.Add(exam => exam.Position);
            options.SearchFields.Add(exam => exam.CandidateVid.ToString());

            options.ToList = exam => TrainingExams.Row(exam, vocabulary, mine: false, mayEdit: false);
            options.ToListPage = (exams, services, _) => services.GetRequiredService<TrainingExams>().RowsAsync(exams);
            options.ToDetail = TrainingExams.Detail;
            options.Apply = TrainingExams.Apply;
        });

        app.MapGet(ChoicesPattern, async (TrainingExams exams, HttpContext http) => Results.Ok(await exams.ChoicesAsync(http.RequestAborted)))
            .WithTags("TrainingExams")
            .WithName("TrainingExamChoices")
            .RequireAuthorization(TrainingPermissions.ManageExams)
            .Produces<ExamChoicesDto>();

        return app;
    }
}
