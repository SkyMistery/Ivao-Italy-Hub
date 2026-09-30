using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Dates;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Modules.Training.Staff;

/// <summary>What a line of the history of a training says happened (A13b, note <c>2026-09-30-lo-storico-di-un-training</c>). Sent by name.</summary>
public enum TrainingHistoryEvent
{
    /// <summary>The trainee asked for it: the row was written.</summary>
    Requested,

    /// <summary>Refused by the hub at the request, because the trainee said the theory exam is not passed.</summary>
    RejectedForTheory,

    Accepted,

    /// <summary>Refused by the staff, with the reason the trainee reads.</summary>
    Rejected,

    /// <summary>Given its first trainer.</summary>
    Assigned,

    /// <summary>Given to another trainer: the one before, and the one after.</summary>
    TrainerChanged,

    /// <summary>
    /// The dates proposed changed: some proposed, or one taken back. The log cannot tell which — a proposal is a row of its own that
    /// the log does not keep, and both steps write the training the same way, touched at the version its writer saw.
    /// </summary>
    DatesChanged,

    /// <summary>Dated by the trainee, among the dates proposed.</summary>
    DateChosen,

    /// <summary>Dated by hand.</summary>
    DateSet,

    /// <summary>Its date moved by hand, from one moment to another.</summary>
    DateMoved,

    /// <summary>The session rescheduled: the training back to its dates.</summary>
    Rescheduled,

    /// <summary>The trainee did not come to the session.</summary>
    NoShow,

    /// <summary>The report published.</summary>
    Completed,

    /// <summary>Closed: by the staff with a reason, or by the hub — nobody — because the trainee chose no date in time.</summary>
    Closed,

    /// <summary>Taken back by the trainee.</summary>
    Cancelled,

    /// <summary>A change whose content the log keeps no longer — an erasure emptied it —, or one this reading has no words for.</summary>
    Changed,

    /// <summary>The trainee's data erased: the training stays in the register, without its texts.</summary>
    Erased,
}

/// <summary>
/// One line of the history as the log says it: who wrote it by VID — none for the hub itself —, what happened, the trainers, the dates
/// and the reason it names. <c>StaffTrainings</c> names the people, as the page names them.
/// </summary>
public sealed record TrainingHistoryLine(
    DateTime At,
    int? ByVid,
    TrainingHistoryEvent Event,
    int? TrainerVid = null,
    int? PreviousTrainerVid = null,
    DateTime? Date = null,
    DateTime? PreviousDate = null,
    string? Reason = null);

/// <summary>
/// The history of a training's changes (A13b; note <c>2026-09-30-lo-storico-di-un-training</c>, Carmine's answer (a) on #197), read
/// from the core's audit log — the rows the one interceptor writes for a <see cref="Training"/>, which is <c>[Audited]</c>, in the
/// same transaction as every write of it. Nothing is written for it: no table, no migration, no second copy; the trainings from
/// before it have their history too.
/// <para>⚠️ <b>The one place of the module that knows the shape of an audit row</b> (the reviewer's condition on #197): a row per
/// save, by the name of the table and the key as text; its <c>Vid</c> the writer, 0 for the hub itself; <c>created</c> with the whole
/// row in <c>AfterJson</c>, <c>updated</c> with the properties that changed, before and after, by their C# names — enums by name,
/// instants in ISO UTC —, and <c>erased</c> with nothing, after an erasure emptied every earlier row of the training. Read with
/// <c>nameof</c> of the properties of <see cref="Training"/>, and only those that say something: state, trainer, date, the trainee's
/// choice, and the reasons. The report's comments stay in the report. <c>TrainingStaffTests.History</c> writes every step through the
/// real interceptor and reads this back: it fails when the shape changes. When a second module wants a history, this reading moves
/// into the core — option (c) of the note — rather than being copied.</para>
/// <para>What the reading has no words for — a row an erasure emptied, an action it does not know — is still a line, <see
/// cref="TrainingHistoryEvent.Changed"/>, with who and when: the history leaves out no row of the log.</para>
/// </summary>
public sealed class TrainingHistory(TrainingDbContext database, HubDbContext hub)
{
    /// <summary>The actions the interceptor writes, as it writes them.</summary>
    private const string Created = "created";

    private const string Updated = "updated";

    private const string ErasedAction = "erased";

    /// <summary>The history of one training, the oldest line first: the order the log was written in.</summary>
    public async Task<IReadOnlyList<TrainingHistoryLine>> ReadAsync(long trainingId, CancellationToken cancellationToken)
    {
        // The table as the model names it: the name the interceptor writes into every row of a training.
        var table = database.Model.FindEntityType(typeof(Training))?.GetTableName() ?? nameof(Training);
        var key = trainingId.ToString(CultureInfo.InvariantCulture);

        var rows = await hub.AuditLog.AsNoTracking()
            .Where(row => row.Entity == table && row.EntityId == key)
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);

        return Read(rows);
    }

    /// <summary>The lines of these rows of the log, in their order; every row is at least one.</summary>
    public static IReadOnlyList<TrainingHistoryLine> Read(IEnumerable<AuditLogEntry> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return [.. rows.SelectMany(Lines)];
    }

    private static IEnumerable<TrainingHistoryLine> Lines(AuditLogEntry row)
    {
        // Nobody signed in wrote it: a job of the hub, which is how the hub closes a training.
        int? by = row.Vid == 0 ? null : row.Vid;

        switch (row.Action)
        {
            case Created:
                // The trainee's request — and, when they said the theory is not passed, the hub's refusal in the same moment.
                yield return new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.Requested);
                if (Parse(row.AfterJson) is { } created
                    && State(created) == TrainingState.Rejected
                    && EnumOf<TrainingRejection>(created, nameof(Training.Rejection)) == TrainingRejection.TheoryNotPassed)
                {
                    yield return new TrainingHistoryLine(row.At, null, TrainingHistoryEvent.RejectedForTheory);
                }

                break;

            case Updated:
                yield return Update(row, by);
                break;

            case ErasedAction:
                yield return new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.Erased);
                break;

            default:
                yield return new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.Changed);
                break;
        }
    }

    /// <summary>
    /// A change, told by what it changed: the state first, which says the step; with the state as it was, the trainer, then the date,
    /// then the proposals — a row that changed nothing but the stamp, which is how <c>TrainingDates</c> writes them.
    /// </summary>
    private static TrainingHistoryLine Update(AuditLogEntry row, int? by)
    {
        if (Parse(row.BeforeJson) is not { } before || Parse(row.AfterJson) is not { } after)
        {
            return new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.Changed);
        }

        if (after.ContainsKey(nameof(Training.State)))
        {
            return State(after) switch
            {
                TrainingState.Accepted => new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.Accepted),
                TrainingState.Rejected => new TrainingHistoryLine(
                    row.At, by, TrainingHistoryEvent.Rejected, Reason: Text(after, nameof(Training.RejectionReason))),
                TrainingState.Assigned when State(before) == TrainingState.Scheduled => new TrainingHistoryLine(
                    row.At, by, TrainingHistoryEvent.Rescheduled, Date: Moment(before, nameof(Training.ScheduledStartUtc))),
                TrainingState.Assigned => new TrainingHistoryLine(
                    row.At, by, TrainingHistoryEvent.Assigned, TrainerVid: Number(after, nameof(Training.TrainerVid))),
                TrainingState.Scheduled => new TrainingHistoryLine(
                    row.At,
                    by,
                    Present(after, nameof(Training.ChosenSlotId)) ? TrainingHistoryEvent.DateChosen : TrainingHistoryEvent.DateSet,
                    Date: Moment(after, nameof(Training.ScheduledStartUtc))),
                TrainingState.Completed => new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.Completed),
                TrainingState.Closed => new TrainingHistoryLine(
                    row.At, by, TrainingHistoryEvent.Closed, Reason: Text(after, nameof(Training.CloseReason))),
                TrainingState.Cancelled => new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.Cancelled),
                TrainingState.NoShow => new TrainingHistoryLine(
                    row.At, by, TrainingHistoryEvent.NoShow, Date: Moment(before, nameof(Training.ScheduledStartUtc))),
                _ => new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.Changed),
            };
        }

        if (after.ContainsKey(nameof(Training.TrainerVid)))
        {
            var previous = Number(before, nameof(Training.TrainerVid));
            var trainer = Number(after, nameof(Training.TrainerVid));
            return previous is null
                ? new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.Assigned, TrainerVid: trainer)
                : new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.TrainerChanged, TrainerVid: trainer, PreviousTrainerVid: previous);
        }

        if (after.ContainsKey(nameof(Training.ScheduledStartUtc)))
        {
            var previous = Moment(before, nameof(Training.ScheduledStartUtc));
            var date = Moment(after, nameof(Training.ScheduledStartUtc));
            return previous is null
                ? new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.DateSet, Date: date)
                : new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.DateMoved, Date: date, PreviousDate: previous);
        }

        return after.Count > 0 && after.All(property => IsStamp(property.Key))
            ? new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.DatesChanged)
            : new TrainingHistoryLine(row.At, by, TrainingHistoryEvent.Changed);
    }

    /// <summary>What the interceptor stamps on every change of a row: who changed it last, and when.</summary>
    private static bool IsStamp(string property) =>
        property is nameof(IAuditable.UpdatedAt) or nameof(IAuditable.UpdatedBy);

    /// <summary>The properties of a row of the log, or none: an erasure empties it, and a text that is not an object says nothing.</summary>
    private static JsonObject? Parse(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TrainingState? State(JsonObject row) => EnumOf<TrainingState>(row, nameof(Training.State));

    /// <summary>An enum as the log writes it: by name. A number is not a name, and is not read.</summary>
    private static TEnum? EnumOf<TEnum>(JsonObject row, string property)
        where TEnum : struct, Enum =>
        row[property] is JsonValue value
        && value.TryGetValue<string>(out var name)
        && Enum.TryParse<TEnum>(name, ignoreCase: false, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : null;

    private static int? Number(JsonObject row, string property) =>
        row[property] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    /// <summary>Whether a property holds something: an identifier, a text, a moment — not nothing.</summary>
    private static bool Present(JsonObject row, string property) => row[property] is JsonValue;

    private static string? Text(JsonObject row, string property) =>
        row[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>An instant as the log writes it, in UTC.</summary>
    private static DateTime? Moment(JsonObject row, string property) =>
        row[property] is JsonValue value && value.TryGetValue<DateTime>(out var moment) ? TrainingDates.Utc(moment) : null;
}
