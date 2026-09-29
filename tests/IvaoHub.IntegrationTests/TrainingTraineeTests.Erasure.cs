using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Data;
using IvaoHub.Core.Ivao;
using IvaoHub.Core.Privacy;
using IvaoHub.Modules.Training;
using IvaoHub.Modules.Training.Bans;
using IvaoHub.Modules.Training.Data;
using IvaoHub.Modules.Training.Dates;
using IvaoHub.Modules.Training.Exams;
using IvaoHub.Modules.Training.Sheets;
using IvaoHub.Modules.Training.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// Erasing a person's data in the training (design M3 §6.1, §10; A12b; note 2026-09-25-la-cancellazione-dei-dati-di-un-trainee), through
/// the real host and the core's erasure, as the superadmin asks it: the register of the trainee's trainings stays, counted the same, under
/// the pseudonym and without a text about them — in its rows and in the audit log —; their open trainings and their exams go, with their
/// entries of the calendar; a ban in force keeps the VID until an erasure run after it is over; a mail to somebody else that names them
/// goes; and a person whose data was erased has no path. What a member of the staff did stays under the pseudonym, with their words.
/// <para>The people are the class's own (A10a). The erasure takes the user of the VID it erases, which the next test seeds again; what
/// stays under a pseudonym no cleaning of the class finds by VID, and each test takes it back (<see cref="CleanErasedAsync"/>).</para>
/// </summary>
public sealed partial class TrainingTraineeTests
{
    /// <summary>The fields of a training that hold what somebody wrote about its trainee (the note's rule): none stays on the register.</summary>
    private static readonly string[] TextsOfATraining = ["availabilityText", "notesText", "rejectionReason", "closeReason", "generalComment", "staffComment"];

    /// <summary>
    /// The test of design §10 on the erasure of a trainee: the register counted the same before and after, no text about them left, their
    /// open trainings and their exams gone with their entries of the calendar, the ban in force kept — and taken by an erasure run again
    /// once it is over; the mail of a date to their trainer, which names them, gone; the staff's pages with a pseudonym and no path for it.
    /// </summary>
    [Fact]
    public async Task AnErasedTraineeLeavesTheRegisterCountedTheSameWithoutTheirTextsAndABanInForce()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, pilot) = Trained();
        var marker = $"trn-test-erasure {Guid.NewGuid():N}";
        var pseudonyms = new List<int>();
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);
        var item = await AddItemAsync(atc, SheetSection.Practice, "erasure", sort: 932, token);

        // The coordinator has a mailbox in this test: the mail of the date fixed names the trainee to them.
        await SeedPersonAsync(CoordinatorVid, "Test", "Coordinator", $"trn-test-{CoordinatorVid}@example.invalid", token, isStaff: true);

        try
        {
            // The register: a training reported with every text on it — the two of the request, the notes of a session rescheduled, the
            // comment and the note on an item, the report's two comments —; one refused with a reason; one the staff closed with a
            // reason; one the trainee cancelled.
            var reported = await ReportedWithNotesAsync(TraineeVid, item, marker, token);
            await WriteAsync<Training>(reported, row => (row.AvailabilityText, row.NotesText) = ($"{marker} evenings", $"{marker} wishes"), token);
            var refused = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Rejected, token);
            await WriteAsync<Training>(refused, row => row.RejectionReason = $"{marker} refused", token);
            var closed = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Closed, token);
            await WriteAsync<Training>(closed, row => (row.ClosedBy, row.ClosedAt, row.CloseReason) = (CoordinatorVid, DateTime.UtcNow, $"{marker} closed"), token);
            var cancelled = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Cancelled, token);
            await WriteAsync<Training>(cancelled, row => (row.ClosedBy, row.ClosedAt) = (TraineeVid, DateTime.UtcNow), token);
            long[] register = [reported, refused, closed, cancelled];

            // Still open: one dated on the ATC path, in the calendar, and one on the pilot path with a date proposed.
            var dated = await AddTrainingAsync(TraineeVid, RatingKind.Atc, TrainingState.Scheduled, token, trainer: CoordinatorVid, start: DateTime.UtcNow.AddDays(2));
            var waiting = await AddTrainingAsync(TraineeVid, RatingKind.Pilot, TrainingState.Assigned, token, trainer: CoordinatorVid);
            var proposed = await AddSlotAsync(waiting, token);

            // An exam where they are the candidate, and another member's, both in the calendar; a ban that holds, and one lifted.
            var theirExam = await AddExamAsync(pilot, TraineeVid, token);
            var anotherExam = await AddExamAsync(pilot, OtherTraineeVid, token);
            var inForce = (await AddBanAsync(TraineeVid, token)).Id;
            var lifted = (await AddBanAsync(TraineeVid, token)).Id;
            await WriteAsync<TraineeBan>(lifted, ban => (ban.Reason, ban.LiftedBy, ban.LiftedAt) = ($"{marker} ban", CoordinatorVid, DateTime.UtcNow), token);

            var before = await RegisterAsync(register, token);
            Assert.True(await InTheCalendarAsync(Training.SourceIdOf(dated), token));
            Assert.True(await InTheCalendarAsync(Exam.SourceIdOf(theirExam), token));
            Assert.True(await AuditMentionsAsync(marker, token), "Without the texts in the audit log, the check below would prove nothing.");
            Assert.Equal(1, await MailsMentioningAsync(CoordinatorVid, TraineeVid, token));

            // What the superadmin reads before confirming, and what the erasure did: the same lines.
            using var superadmin = await SignedInAsync(SuperadminVid, token);
            (string Key, int Count, string Outcome)[] lines =
            [
                ("training:erasure.closed", 4, nameof(ErasureOutcome.Anonymised)),
                ("training:erasure.open", 2, nameof(ErasureOutcome.Deleted)),
                ("training:erasure.exams", 1, nameof(ErasureOutcome.Deleted)),
                ("training:erasure.bansInForce", 1, nameof(ErasureOutcome.Kept)),
                ("training:erasure.bansOver", 1, nameof(ErasureOutcome.Anonymised)),
            ];
            Assert.Equal(lines, TrainingLines(await superadmin.GetFromJsonAsync<JsonElement>(new Uri($"{ErasureEndpoints.Pattern}/{TraineeVid}", UriKind.Relative), token)));

            var erased = await EraseAsync(superadmin, TraineeVid, token);
            var pseudonym = erased.GetProperty("pseudonym").GetInt32();
            pseudonyms.Add(pseudonym);
            Assert.Equal(lines, TrainingLines(erased));

            // The register, counted the same, and about nobody: its trainee is the pseudonym, and so is whoever cancelled one; the staff's
            // closing still says who closed it.
            Assert.Equal(before, await RegisterAsync(register, token));
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
                var kept = await database.Trainings.IgnoreQueryFilters().AsNoTracking().Where(row => register.Contains(row.Id)).ToDictionaryAsync(row => row.Id, token);
                Assert.All(kept.Values, row => Assert.Equal(pseudonym, row.TraineeVid));
                Assert.Equal((int?)pseudonym, kept[cancelled].ClosedBy);
                Assert.Equal((int?)CoordinatorVid, kept[closed].ClosedBy);

                // Nothing about them in the rows: not in the trainings, not in their sessions and sheets, not in a ban.
                Assert.False(await database.Trainings.IgnoreQueryFilters().AnyAsync(
                    row => row.AvailabilityText!.Contains(marker) || row.NotesText!.Contains(marker) || row.RejectionReason!.Contains(marker)
                        || row.CloseReason!.Contains(marker) || row.GeneralComment!.Contains(marker) || row.StaffComment!.Contains(marker),
                    token));
                Assert.False(await database.Sessions.AnyAsync(row => row.InternalNotes!.Contains(marker), token));
                Assert.False(await database.Evaluations.AnyAsync(row => row.TraineeComment!.Contains(marker) || row.StaffNote!.Contains(marker), token));
                Assert.False(await database.TraineeBans.IgnoreQueryFilters().AnyAsync(row => row.Reason.Contains(marker), token));

                // The open trainings are gone, with the date proposed; the exam where they were the candidate too, and not another's.
                Assert.False(await database.Trainings.IgnoreQueryFilters().AnyAsync(row => row.Id == dated || row.Id == waiting, token));
                Assert.False(await database.Slots.AnyAsync(row => row.Id == proposed, token));
                Assert.Equal([anotherExam], await database.Exams.IgnoreQueryFilters().Where(row => row.Id == theirExam || row.Id == anotherExam).Select(row => row.Id).ToListAsync(token));

                // The ban in force as it was; the one lifted without them and without its reason.
                var bans = await database.TraineeBans.IgnoreQueryFilters().AsNoTracking().Where(row => row.Id == inForce || row.Id == lifted).ToDictionaryAsync(row => row.Id, token);
                Assert.Equal((TraineeVid, "trn-test: written by the installation"), (bans[inForce].Vid, bans[inForce].Reason));
                Assert.Equal((pseudonym, string.Empty), (bans[lifted].Vid, bans[lifted].Reason));
            }

            // Their entries of the calendar left through the interceptor (A8a), and nothing about them in the audit log or in a mail.
            Assert.False(await InTheCalendarAsync(Training.SourceIdOf(dated), token));
            Assert.False(await InTheCalendarAsync(Exam.SourceIdOf(theirExam), token));
            Assert.True(await InTheCalendarAsync(Exam.SourceIdOf(anotherExam), token));
            Assert.False(await AuditMentionsAsync(marker, token));
            Assert.Equal(0, await MailsMentioningAsync(CoordinatorVid, TraineeVid, token));
            Assert.Equal(0, await MailsMentioningAsync(TraineeVid, TraineeVid, token));

            // The staff's pages: the training reported, about a person nobody can name, and without a word about them; its sheet keeps
            // the grade. The closing of the staff still says who.
            using var reader = await SignedInAsync(ReaderVid, token);
            var page = await PageAsync(reader, reported, token);
            Assert.Equal((pseudonym, JsonValueKind.Null), (page.GetProperty("trainee").GetProperty("vid").GetInt32(), page.GetProperty("trainee").GetProperty("name").ValueKind));
            Assert.Equal(CoordinatorVid, page.GetProperty("trainer").GetProperty("vid").GetInt32());
            Assert.All(TextsOfATraining, field => Assert.Equal(JsonValueKind.Null, page.GetProperty(field).ValueKind));
            var graded = page.GetProperty("sheet").EnumerateArray().Single(each => each.GetProperty("itemId").GetInt64() == item);
            Assert.Equal((3, JsonValueKind.Null, JsonValueKind.Null), (graded.GetProperty("grade").GetInt32(), graded.GetProperty("traineeComment").ValueKind, graded.GetProperty("staffNote").ValueKind));
            Assert.All(page.GetProperty("sessions").EnumerateArray(), session => Assert.Equal(JsonValueKind.Null, session.GetProperty("internalNotes").ValueKind));
            Assert.Equal(CoordinatorVid, (await PageAsync(reader, closed, token)).GetProperty("closedBy").GetProperty("vid").GetInt32());

            // No path for a pseudonym. The VID's own still answers, for the ban it keeps.
            using (var none = await reader.GetAsync(new Uri($"{TraineePathEndpoints.Pattern}/{pseudonym}", UriKind.Relative), token))
            {
                Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
            }

            var path = await PathAsync(reader, TraineeVid, token);
            Assert.Equal((0, JsonValueKind.Null), (path.GetProperty("trainings").GetArrayLength(), path.GetProperty("trainee").GetProperty("name").ValueKind));
            Assert.Equal(inForce, Assert.Single(path.GetProperty("bans").EnumerateArray()).GetProperty("id").GetInt64());

            // Run again once the ban is over: it goes too, and nothing is left of the VID.
            await EndBanAsync(inForce, token);
            var again = await EraseAsync(superadmin, TraineeVid, token);
            var second = again.GetProperty("pseudonym").GetInt32();
            pseudonyms.Add(second);
            Assert.Equal(
                new (string, int, string)[]
                {
                    ("training:erasure.closed", 0, nameof(ErasureOutcome.Anonymised)),
                    ("training:erasure.open", 0, nameof(ErasureOutcome.Deleted)),
                    ("training:erasure.exams", 0, nameof(ErasureOutcome.Deleted)),
                    ("training:erasure.bansInForce", 0, nameof(ErasureOutcome.Kept)),
                    ("training:erasure.bansOver", 1, nameof(ErasureOutcome.Anonymised)),
                },
                TrainingLines(again));
            var ended = await StoredBanAsync(inForce, token);
            Assert.Equal((second, string.Empty), (ended.Vid, ended.Reason));
            using (var gone = await reader.GetAsync(new Uri($"{TraineePathEndpoints.Pattern}/{TraineeVid}", UriKind.Relative), token))
            {
                Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
            }
        }
        finally
        {
            await CleanErasedAsync(pseudonyms, CancellationToken.None);
        }
    }

    /// <summary>
    /// Answer 4 of the note of T20b, in the training: a member of the staff who is erased leaves what they did — a training decided,
    /// assigned, conducted and reported, a ban given — under the pseudonym, and their words with it, which are about others. The pages say
    /// who by the pseudonym, the ban's giver too. The training's own lines say it holds nothing about them.
    /// </summary>
    [Fact]
    public async Task AnErasedStaffMemberLeavesTheirWorkUnderThePseudonymWithTheirWords()
    {
        var token = TestContext.Current.CancellationToken;
        var (atc, _) = Trained();
        var marker = $"trn-test-erasure {Guid.NewGuid():N}";
        await WriteSettingsAsync(new { conflictPolicy = "None" }, token);
        var item = await AddItemAsync(atc, SheetSection.Practice, "erasure of the staff", sort: 933, token);

        // The coordinator decides, assigns, conducts and reports a training of the trainee's, and bans another member.
        var reported = await ReportedWithNotesAsync(TraineeVid, item, marker, token);
        using (var coordinator = await SignedInAsync(CoordinatorVid, token))
        {
            await ReadAsync(await BanAsync(coordinator, OtherTraineeVid, $"{marker} ban", endsAt: null, token), HttpStatusCode.Created, token);
        }

        using var superadmin = await SignedInAsync(SuperadminVid, token);
        var erased = await EraseAsync(superadmin, CoordinatorVid, token);
        var pseudonym = erased.GetProperty("pseudonym").GetInt32();
        Assert.All(TrainingLines(erased), line => Assert.Equal(0, line.Count));

        // Their work, by the pseudonym, with their words: the report's comments, the notes of the session and of the item.
        using var reader = await SignedInAsync(ReaderVid, token);
        var page = await PageAsync(reader, reported, token);
        Assert.Equal(
            (TraineeVid, pseudonym, pseudonym, pseudonym),
            (Vid(page, "trainee"), Vid(page, "trainer"), Vid(page, "decidedBy"), Vid(page, "assignedBy")));
        Assert.Equal(JsonValueKind.Null, page.GetProperty("trainer").GetProperty("name").ValueKind);
        Assert.Equal(
            [$"trn-test-reserved: {marker}, staff", $"trn-test-reserved: {marker}, item", $"trn-test-reserved: {marker}, session"],
            ReservedValues(page, item));
        Assert.Equal($"trn-test: {marker}, general", page.GetProperty("generalComment").GetString());
        Assert.All(page.GetProperty("sessions").EnumerateArray(), session => Assert.Equal(pseudonym, Vid(session, "recordedBy")));

        // The ban they gave, with its reason, given by the deleted person (not by nobody).
        var ban = Assert.Single(await ReadListAsync(reader, $"{BanEndpoints.Pattern}?filter[vid]={OtherTraineeVid}", token));
        Assert.Equal(($"{marker} ban", pseudonym), (ban.GetProperty("reason").GetString(), Vid(ban, "givenBy")));
        Assert.Equal(JsonValueKind.Null, ban.GetProperty("givenBy").GetProperty("name").ValueKind);
    }

    // ---- helpers of the erasure --------------------------------------------------------------------------------------------

    private static int Vid(JsonElement answer, string member) => answer.GetProperty(member).GetProperty("vid").GetInt32();

    /// <summary>The erasure of a VID, asked by the superadmin; the answer, with the pseudonym and the lines.</summary>
    private static async Task<JsonElement> EraseAsync(HttpClient superadmin, int vid, CancellationToken cancellationToken) =>
        await ReadAsync(
            await superadmin.PostAsync(new Uri($"{ErasureEndpoints.Pattern}/{vid}", UriKind.Relative), content: null, cancellationToken),
            HttpStatusCode.OK,
            cancellationToken);

    /// <summary>The training's lines of an erasure's preview or result: what, how many, and what becomes of them.</summary>
    private static (string Key, int Count, string Outcome)[] TrainingLines(JsonElement answer) =>
    [
        .. answer.GetProperty("lines").EnumerateArray()
            .Where(line => line.GetProperty("key").GetString()!.StartsWith($"{TrainingModule.ModuleKey}:", StringComparison.Ordinal))
            .Select(line => (line.GetProperty("key").GetString()!, line.GetProperty("count").GetInt32(), line.GetProperty("outcome").GetString()!)),
    ];

    /// <summary>
    /// What the register says of these trainings, and of their sessions and sheets, without whom they are about: the states, dates,
    /// ratings, grades and boxes the counts of the department read. The same before and after an erasure.
    /// </summary>
    private async Task<List<string>> RegisterAsync(long[] ids, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();

        var trainings = await database.Trainings.IgnoreQueryFilters().AsNoTracking().Where(row => ids.Contains(row.Id)).OrderBy(row => row.Id).ToListAsync(cancellationToken);
        var sessions = await database.Sessions.AsNoTracking().Where(row => ids.Contains(row.TrainingId)).OrderBy(row => row.Id).ToListAsync(cancellationToken);
        var sheets = await database.Evaluations.AsNoTracking().Where(row => ids.Contains(row.TrainingId)).OrderBy(row => row.Id).ToListAsync(cancellationToken);

        return
        [
            .. trainings.Select(row => string.Create(
                CultureInfo.InvariantCulture,
                $"{row.Id} {row.Kind} {row.Rating} {row.State} {row.Rejection} {row.IsMockExam} {row.Position} trainer {row.TrainerVid} decided {row.DecidedBy} {row.DecidedAt:O} assigned {row.AssignedBy} {row.ScheduledStartUtc:O} {row.CompletedAt:O} {row.ClosedAt:O} {row.ReadyForMockExam} {row.ReadyForExam} {row.CooldownWaived} {row.TraineeRatingAtRequest} {row.TraineeHoursAtRequest}")),
            .. sessions.Select(row => string.Create(CultureInfo.InvariantCulture, $"session {row.TrainingId} {row.StartsAtUtc:O} {row.Outcome} {row.CreatedBy}")),
            .. sheets.Select(row => string.Create(CultureInfo.InvariantCulture, $"item {row.TrainingId} {row.SheetItemId} {row.Section} {row.Grade} {row.Mark}")),
        ];
    }

    /// <summary>Writes a row of the training as the installation: what a test needs on it that no step of the API writes.</summary>
    private async Task WriteAsync<TEntity>(long id, Action<TEntity> write, CancellationToken cancellationToken)
        where TEntity : class
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        write(await database.Set<TEntity>().IgnoreQueryFilters().SingleAsync(row => EF.Property<long>(row, "Id") == id, cancellationToken));
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A date proposed for a training in three days, written as the installation.</summary>
    private async Task<long> AddSlotAsync(long training, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var start = DateTime.UtcNow.AddDays(3);
        var slot = new TrainingSlot { TrainingId = training, StartsAtUtc = start, EndsAtUtc = start.AddHours(2) };
        database.Slots.Add(slot);
        await database.SaveChangesAsync(cancellationToken);
        return slot.Id;
    }

    /// <summary>An exam in five days, held by the coordinator, written as the installation: it goes into the calendar.</summary>
    private async Task<long> AddExamAsync(Rating rating, int candidate, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var exam = new Exam
        {
            Kind = rating.Kind,
            Rating = rating.Number,
            StartsAtUtc = DateTime.UtcNow.AddDays(5),
            CandidateVid = candidate,
            ExaminerVid = CoordinatorVid,
        };
        database.Exams.Add(exam);
        await database.SaveChangesAsync(cancellationToken);
        return exam.Id;
    }

    private async Task<bool> InTheCalendarAsync(string source, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().CalendarEntries.IgnoreQueryFilters()
            .AnyAsync(entry => entry.SourceModule == TrainingModule.ModuleKey && entry.SourceId == source, cancellationToken);
    }

    /// <summary>Whether a row of the audit log still copies a text with the marker in it, before or after its change.</summary>
    private async Task<bool> AuditMentionsAsync(string marker, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().AuditLog
            .AnyAsync(row => row.BeforeJson!.Contains(marker) || row.AfterJson!.Contains(marker), cancellationToken);
    }

    /// <summary>The mails to <paramref name="recipient"/> whose data names <paramref name="named"/>, by their VID in any of its words.</summary>
    private async Task<int> MailsMentioningAsync(int recipient, int named, CancellationToken cancellationToken)
    {
        var text = named.ToString(CultureInfo.InvariantCulture);
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HubDbContext>().Notifications
            .CountAsync(notification => notification.Vid == recipient && notification.DataJson.Contains(text), cancellationToken);
    }

    /// <summary>
    /// What an erasure left under its pseudonyms, which the cleaning of the class does not find by VID: the trainings of the register with
    /// their entries of the calendar — their sessions and sheets go with them —, and the bans.
    /// </summary>
    private async Task CleanErasedAsync(List<int> pseudonyms, CancellationToken cancellationToken)
    {
        if (pseudonyms.Count == 0)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var training = scope.ServiceProvider.GetRequiredService<TrainingDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var sources = (await training.Trainings.IgnoreQueryFilters().Where(row => pseudonyms.Contains(row.TraineeVid)).Select(row => row.Id).ToListAsync(cancellationToken))
            .Select(Training.SourceIdOf)
            .ToList();
        await hub.CalendarEntries.IgnoreQueryFilters()
            .Where(entry => entry.SourceModule == TrainingModule.ModuleKey && sources.Contains(entry.SourceId))
            .ExecuteDeleteAsync(cancellationToken);
        await training.Trainings.IgnoreQueryFilters().Where(row => pseudonyms.Contains(row.TraineeVid)).ExecuteDeleteAsync(cancellationToken);
        await training.TraineeBans.IgnoreQueryFilters().Where(ban => pseudonyms.Contains(ban.Vid)).ExecuteDeleteAsync(cancellationToken);
    }
}
