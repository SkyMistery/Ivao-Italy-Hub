using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Notifications;
using IvaoHub.Modules.FlightOps;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The validation through the real host (M2, T13a): the queue, the take with its lease, the decision with the errors of the
/// frozen rules and the suggestion, the mails without the validator's name, the reopening, the digest and the tracks. The
/// validator of these tests holds <c>Tours.Validate</c> on <b>one</b> tour and nothing else: every write of theirs passes the
/// interceptor's guard through <c>AlsoWrittenWith</c>, which is what these tests prove about the core.
/// </summary>
public sealed partial class PirepTests
{
    // T13 takes 85–87.
    private const int ValidatorVid = 780085;
    private const int SecondValidatorVid = 780086;
    private const int SuperadminPilotVid = 780087;

    private readonly List<long> _errors = [];

    /// <summary>
    /// The whole cycle: the queue, a take, «to modify» with its note and mail, the correction back in the queue for anybody,
    /// a rejection on a dangerous error — suggested, so no reason asked — that the pilot reads with the rule broken and
    /// never the name, a reopening by whoever decided, and an acceptance against the suggestion, which needs a reason and
    /// replaces the errors.
    /// </summary>
    [Fact]
    public async Task AReportGoesThroughEveryStateOfTheReview()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (dangerous, _, code) = await RuleWithErrorsAsync(coordinator, tourId, yearlyMax: 3, token);
        await GrantValidateAsync(ValidatorVid, tourId, token);
        using var validator = await SignedInAsync(ValidatorVid, token);

        var flown = _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1));
        var sent = await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], flown), token);
        var id = Id(sent);

        // In the queue of the tour, takable by the validator enabled on it.
        var row = await QueueRowAsync(validator, tourId, id, token);
        Assert.Equal("Queued", row.GetProperty("status").GetString());
        Assert.True(row.GetProperty("canTake").GetBoolean());
        Assert.False(row.GetProperty("isOwn").GetBoolean());

        var page = await TakeAsync(validator, id, token);
        Assert.Equal("InReview", page.GetProperty("status").GetString());
        Assert.Equal(ValidatorVid, page.GetProperty("assignedTo").GetProperty("vid").GetInt32());
        Assert.True(page.GetProperty("actions").GetProperty("canDecide").GetBoolean());
        Assert.True(Assert.Single(page.GetProperty("flights").EnumerateArray()).GetProperty("hasTrack").GetBoolean());

        // Held: nobody else takes it while the lease runs (§4.2).
        await RefusedStepAsync(coordinator, id, "take", new ReviewStepDto(RowVersion(page)), "status", "flightops:errors.reviewTaken", token);

        // «To modify» says what to correct.
        var toModify = Decision(PirepStatus.ToModify, [], RowVersion(page));
        await RefusedStepAsync(validator, id, "decide", toModify, "noteToPilot", "flightops:errors.reviewToModifyNeedsNote", token);
        page = await StepAsync(validator, id, "decide", toModify with { NoteToPilot = "fo-test the SID is missing" }, token);
        Assert.Equal("ToModify", page.GetProperty("status").GetString());

        var mail = await MailAsync(PilotVid, FlightOpsNotifications.PirepToModify, token);
        Assert.Contains("fo-test the SID is missing", mail.DataJson, StringComparison.Ordinal);
        Assert.DoesNotContain(ValidatorVid.ToString(CultureInfo.InvariantCulture), mail.DataJson, StringComparison.Ordinal);

        // The pilot corrects it: back in the queue, held by nobody (§3.1).
        var read = await OkAsync(await pilot.GetAsync($"{PirepEndpoints.Pattern}/{id}", token), token);
        Assert.Equal("fo-test the SID is missing", read.GetProperty("noteToPilot").GetString());
        await OkAsync(
            await pilot.PutAsJsonAsync($"{PirepEndpoints.Pattern}/{id}", Payload(legs[0], flown) with { RowVersion = read.GetProperty("rowVersion").GetDateTime() }, token),
            token);
        page = await OkAsync(await validator.GetAsync($"{ReviewEndpoints.Pattern}/{id}", token), token);
        Assert.Equal("Queued", page.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("assignedTo").ValueKind);

        // A rejection needs an error; with a dangerous one the suggestion agrees, so no reason is asked.
        page = await TakeAsync(validator, id, token);
        await RefusedStepAsync(validator, id, "decide", Decision(PirepStatus.Rejected, [], RowVersion(page)), "errorIds", "flightops:errors.reviewRejectNeedsError", token);
        await RefusedStepAsync(validator, id, "decide", Decision(PirepStatus.Rejected, [-1], RowVersion(page)), "errorIds", "flightops:errors.reviewErrorUnknown", token);

        var suggestion = await OkAsync(await validator.GetAsync($"{ReviewEndpoints.Pattern}/{id}/suggestion?errorIds={dangerous}", token), token);
        Assert.Equal("Rejected", suggestion.GetProperty("outcome").GetString());
        Assert.Equal(ReviewSuggestion.Dangerous, Assert.Single(suggestion.GetProperty("reasons").EnumerateArray()).GetProperty("reason").GetString());

        page = await StepAsync(validator, id, "decide", Decision(PirepStatus.Rejected, [dangerous], RowVersion(page)), token);
        Assert.Equal("Rejected", page.GetProperty("status").GetString());
        Assert.False(page.GetProperty("thresholdOverridden").GetBoolean());
        Assert.True(page.GetProperty("errors").EnumerateArray().Single(error => error.GetProperty("id").GetInt64() == dangerous).GetProperty("marked").GetBoolean());

        mail = await MailAsync(PilotVid, FlightOpsNotifications.PirepRejected, token);
        Assert.Contains(code, mail.DataJson, StringComparison.Ordinal);
        Assert.DoesNotContain(ValidatorVid.ToString(CultureInfo.InvariantCulture), mail.DataJson, StringComparison.Ordinal);

        read = await OkAsync(await pilot.GetAsync($"{PirepEndpoints.Pattern}/{id}", token), token);
        Assert.Equal(code, Assert.Single(read.GetProperty("violatedRules").EnumerateArray()).GetProperty("code").GetString());
        Assert.False(read.TryGetProperty("decidedBy", out _));

        // Whoever decided reopens it, with a reason; it is theirs again.
        await RefusedStepAsync(validator, id, "reopen", new ReviewReopenDto(" ", RowVersion(page)), "reason", "errors.required", token);
        page = await StepAsync(validator, id, "reopen", new ReviewReopenDto("fo-test looked again", RowVersion(page)), token);
        Assert.Equal("InReview", page.GetProperty("status").GetString());
        Assert.Equal(ValidatorVid, page.GetProperty("assignedTo").GetProperty("vid").GetInt32());
        Assert.Contains(page.GetProperty("history").EnumerateArray(), step => step.GetProperty("note").GetString() == "fo-test looked again");

        // Accepting with the dangerous error still marked goes against the suggestion: only with a reason.
        var accept = Decision(PirepStatus.Accepted, [dangerous], RowVersion(page));
        await RefusedStepAsync(validator, id, "decide", accept, "overrideReason", "flightops:errors.reviewOverrideNeedsReason", token);
        page = await StepAsync(validator, id, "decide", accept with { OverrideReason = "fo-test the tower cleared it" }, token);
        Assert.Equal("Accepted", page.GetProperty("status").GetString());
        Assert.True(page.GetProperty("thresholdOverridden").GetBoolean());
        await MailAsync(PilotVid, FlightOpsNotifications.PirepAccepted, token);

        // The decision replaced the errors: one row, not two.
        await using var scope = _host.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
        Assert.Equal(1, await database.PirepErrors.CountAsync(error => error.PirepId == id, token));
    }

    /// <summary>
    /// Nobody validates their own reports (§7.3), super administrator included: they see them in the queue, read only.
    /// </summary>
    [Fact]
    public async Task NobodyTakesTheirOwnReportNotEvenASuperAdministrator()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);

        foreach (var vid in new[] { CoordinatorVid, SuperadminPilotVid })
        {
            using var client = vid == CoordinatorVid ? null : await SignedInAsync(vid, token);
            var who = client ?? coordinator;
            var flown = _flights.Add(vid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1));
            var sent = await CreatedAsync(who, Reports(tourId), Payload(legs[0], flown), token);

            var row = await QueueRowAsync(who, tourId, Id(sent), token);
            Assert.True(row.GetProperty("isOwn").GetBoolean());
            Assert.False(row.GetProperty("canTake").GetBoolean());

            var page = await OkAsync(await who.GetAsync($"{ReviewEndpoints.Pattern}/{Id(sent)}", token), token);
            Assert.False(page.GetProperty("actions").GetProperty("canTake").GetBoolean());

            using var taken = await who.PostAsJsonAsync($"{ReviewEndpoints.Pattern}/{Id(sent)}/take", new ReviewStepDto(RowVersion(page)), token);
            Assert.Equal(HttpStatusCode.Forbidden, taken.StatusCode);
        }
    }

    /// <summary>
    /// A validator enabled on one tour takes its reports and not another's (§7.3), though they read every report (§4.1);
    /// and two takes at once: the first wins (§4.2).
    /// </summary>
    [Fact]
    public async Task AValidatorEnabledOnOneTourTakesOnlyItsReportsAndTheFirstTakeWins()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (enabled, enabledLegs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (other, otherLegs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        await GrantValidateAsync(ValidatorVid, enabled, token);
        await GrantValidateAsync(SecondValidatorVid, enabled, token);
        using var validator = await SignedInAsync(ValidatorVid, token);
        using var second = await SignedInAsync(SecondValidatorVid, token);

        var mine = Id(await CreatedAsync(pilot, Reports(enabled), Payload(enabledLegs[0], _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1))), token));
        var theirs = Id(await CreatedAsync(pilot, Reports(other), Payload(otherLegs[0], _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1).AddHours(-3))), token));

        // The other tour: read, not taken.
        var row = await QueueRowAsync(validator, other, theirs, token);
        Assert.False(row.GetProperty("canTake").GetBoolean());
        var page = await OkAsync(await validator.GetAsync($"{ReviewEndpoints.Pattern}/{theirs}", token), token);
        using (var refused = await validator.PostAsJsonAsync($"{ReviewEndpoints.Pattern}/{theirs}/take", new ReviewStepDto(RowVersion(page)), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // Two at once on the same version: one of them holds it, the other is told it is taken or has changed.
        page = await OkAsync(await validator.GetAsync($"{ReviewEndpoints.Pattern}/{mine}", token), token);
        var body = new ReviewStepDto(RowVersion(page));
        var answers = await Task.WhenAll(
            validator.PostAsJsonAsync($"{ReviewEndpoints.Pattern}/{mine}/take", body, token),
            second.PostAsJsonAsync($"{ReviewEndpoints.Pattern}/{mine}/take", body, token));
        try
        {
            Assert.Single(answers, answer => answer.StatusCode == HttpStatusCode.OK);
            Assert.Single(answers, answer => answer.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest);
        }
        finally
        {
            foreach (var answer in answers)
            {
                answer.Dispose();
            }
        }

        page = await OkAsync(await validator.GetAsync($"{ReviewEndpoints.Pattern}/{mine}", token), token);
        Assert.Single(page.GetProperty("history").EnumerateArray(), step => step.GetProperty("toStatus").GetString() == "InReview");
    }

    /// <summary>
    /// A warning that takes the pilot's year over its maximum suggests a rejection (§4.3): the count is the error confirmed on
    /// their decided reports of the year, on any tour, plus this one.
    /// </summary>
    [Fact]
    public async Task AWarningOverItsYearlyMaximumSuggestsARejection()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (_, warning, _) = await RuleWithErrorsAsync(coordinator, tourId, yearlyMax: 1, token);

        var first = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1).AddHours(-5))), token));

        // The first time is within the maximum: accepted, with the warning marked.
        var suggestion = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{first}/suggestion?errorIds={warning}", token), token);
        Assert.Equal("Accepted", suggestion.GetProperty("outcome").GetString());
        var page = await TakeAsync(coordinator, first, token);
        await StepAsync(coordinator, first, "decide", Decision(PirepStatus.Accepted, [warning], RowVersion(page)), token);

        // The second, the same year: one over.
        var second = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legs[1], _flights.Add(PilotVid, "XAA100", Milan, London, DateTime.UtcNow.AddDays(-1))), token));
        suggestion = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{second}/suggestion?errorIds={warning}", token), token);
        Assert.Equal("Rejected", suggestion.GetProperty("outcome").GetString());
        var reason = Assert.Single(suggestion.GetProperty("reasons").EnumerateArray());
        Assert.Equal(ReviewSuggestion.OverYearlyMax, reason.GetProperty("reason").GetString());
        Assert.Equal(2, reason.GetProperty("countInYear").GetInt32());

        page = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{second}", token), token);
        var tally = page.GetProperty("errors").EnumerateArray().Single(error => error.GetProperty("id").GetInt64() == warning);
        Assert.Equal(1, tally.GetProperty("countInYear").GetInt32());
        Assert.Equal(1, tally.GetProperty("countEver").GetInt32());
    }

    /// <summary>
    /// The daily digest (§4.2.2) goes to a validator with the tours they may validate, and not to one whose tours have nothing
    /// waiting; the track stored at the send goes <c>trackRetentionDays</c> after the decision (note 2026-09-23-la-validazione).
    /// </summary>
    [Fact]
    public async Task TheDigestGoesOnlyToWhoHasSomethingToDoAndOldTracksGo()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (quiet, _) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        await GrantValidateAsync(ValidatorVid, tourId, token);
        await GrantValidateAsync(SecondValidatorVid, quiet, token);

        var id = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1))), token));

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var since = DateTime.UtcNow.AddSeconds(-1);
            await scope.ServiceProvider.GetRequiredService<ReviewDigestJob>().RunAsync(token);

            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var digests = await hub.Notifications.AsNoTracking()
                .Where(row => row.Type == FlightOpsNotifications.ReviewDigest && row.CreatedAt >= since)
                .ToListAsync(token);
            var theirs = Assert.Single(digests, row => row.Vid == ValidatorVid);
            Assert.Contains($"fo-test-pirep", theirs.DataJson, StringComparison.Ordinal);
            Assert.DoesNotContain(digests, row => row.Vid == SecondValidatorVid);
        }

        // Decided long ago: the track goes, the report stays.
        using var validator = await SignedInAsync(ValidatorVid, token);
        var page = await TakeAsync(validator, id, token);
        await StepAsync(validator, id, "decide", Decision(PirepStatus.Accepted, [], RowVersion(page)), token);
        var tracks = await OkAsync(await validator.GetAsync($"{ReviewEndpoints.Pattern}/{id}/tracks", token), token);
        Assert.Equal(3, Assert.Single(tracks.EnumerateArray()).GetProperty("points").GetArrayLength());

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
            await Everything<Pirep>(database).Where(report => report.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(report => report.DecidedAt, DateTime.UtcNow.AddDays(-91)), token);
            Assert.True(await scope.ServiceProvider.GetRequiredService<TrackRetentionJob>().RunAsync(token) >= 1);
        }

        tracks = await OkAsync(await validator.GetAsync($"{ReviewEndpoints.Pattern}/{id}/tracks", token), token);
        Assert.Equal(JsonValueKind.Null, Assert.Single(tracks.EnumerateArray()).GetProperty("points").ValueKind);
        page = await OkAsync(await validator.GetAsync($"{ReviewEndpoints.Pattern}/{id}", token), token);
        Assert.False(Assert.Single(page.GetProperty("flights").EnumerateArray()).GetProperty("hasTrack").GetBoolean());
    }

    private static ReviewDecisionDto Decision(PirepStatus outcome, long[] errors, DateTime rowVersion) =>
        new(outcome, errors, NoteToPilot: null, StaffNote: null, OverrideReason: null, rowVersion);

    private static DateTime RowVersion(JsonElement page) => page.GetProperty("rowVersion").GetDateTime();

    private async Task<JsonElement> TakeAsync(HttpClient client, long id, CancellationToken cancellationToken)
    {
        var page = await OkAsync(await client.GetAsync($"{ReviewEndpoints.Pattern}/{id}", cancellationToken), cancellationToken);
        return await StepAsync(client, id, "take", new ReviewStepDto(RowVersion(page)), cancellationToken);
    }

    private static async Task<JsonElement> StepAsync<T>(HttpClient client, long id, string step, T body, CancellationToken cancellationToken) =>
        await OkAsync(await client.PostAsJsonAsync($"{ReviewEndpoints.Pattern}/{id}/{step}", body, cancellationToken), cancellationToken);

    private static async Task RefusedStepAsync<T>(
        HttpClient client,
        long id,
        string step,
        T body,
        string field,
        string key,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync($"{ReviewEndpoints.Pattern}/{id}/{step}", body, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{response.StatusCode}: {text}");

        var errors = JsonDocument.Parse(text).RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out var keys) && keys.EnumerateArray().Any(item => item.GetString() == key), text);
    }

    private static async Task<JsonElement> QueueRowAsync(HttpClient client, long tourId, long id, CancellationToken cancellationToken)
    {
        var page = await OkAsync(
            await client.GetAsync($"{ReviewEndpoints.QueuePattern}?filter[tourId]={tourId}&pageSize=100", cancellationToken),
            cancellationToken);
        return page.GetProperty("items").EnumerateArray().Single(row => row.GetProperty("id").GetInt64() == id);
    }

    /// <summary>The last mail of a type queued for a member, written by this test's decision.</summary>
    private async Task<Notification> MailAsync(int vid, string type, CancellationToken cancellationToken)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        return await hub.Notifications.AsNoTracking()
            .Where(row => row.Vid == vid && row.Type == type)
            .OrderByDescending(row => row.Id)
            .FirstAsync(cancellationToken);
    }

    /// <summary>A rule of the tour with a dangerous error and a warning of the given yearly maximum.</summary>
    private async Task<(long Dangerous, long Warning, string Code)> RuleWithErrorsAsync(
        HttpClient coordinator,
        long tourId,
        int yearlyMax,
        CancellationToken cancellationToken)
    {
        var dangerous = Id(await CreatedAsync(
            coordinator,
            RuleEndpoints.ErrorsPattern,
            new TourErrorWriteDto(Text("fo-test dangerous"), Text("fo-test"), null, ErrorCategory.Dangerous, null, null, false, false, default),
            cancellationToken));
        var warning = Id(await CreatedAsync(
            coordinator,
            RuleEndpoints.ErrorsPattern,
            new TourErrorWriteDto(Text("fo-test warning"), Text("fo-test"), null, ErrorCategory.Warning, yearlyMax, null, false, false, default),
            cancellationToken));
        _errors.AddRange([dangerous, warning]);

        var code = $"V{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var rule = await CreatedAsync(
            coordinator,
            RuleEndpoints.RulesPattern,
            new TourRuleWriteDto(tourId, code, Text(code), Text($"The rule {code}."), null, null, null, [dangerous, warning], 0, false, default),
            cancellationToken);
        _rules.Add(Id(rule));

        return (dangerous, warning, code);
    }

    /// <summary>What «add validator» will write (T15): <c>Tours.Validate</c> on one tour, to one member.</summary>
    private async Task GrantValidateAsync(int vid, long tourId, CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        database.UserGrants.Add(new UserGrant
        {
            Vid = vid,
            Kind = GrantKind.Permission,
            Value = TourPermissions.Validate,
            Department = Department.FOD,
            ResourceScope = Pirep.ScopeOf(tourId),
            Effect = GrantEffect.Grant,
            Reason = "fo-test",
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Two validators with no position and no other permission, a super administrator who flies, and addresses for the mails.</summary>
    private async Task SeedReviewersAsync(CancellationToken cancellationToken)
    {
        await SeedUserAsync(ValidatorVid, staffPosition: null, rating: 4, cancellationToken);
        await SeedUserAsync(SecondValidatorVid, staffPosition: null, rating: 4, cancellationToken);
        await SeedUserAsync(SuperadminPilotVid, staffPosition: null, rating: 4, cancellationToken);
        await CleanReviewersAsync(cancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var vids = new[] { PilotVid, ValidatorVid, SecondValidatorVid, CoordinatorVid };
        foreach (var user in await database.Users.Where(user => vids.Contains(user.Vid)).ToListAsync(cancellationToken))
        {
            user.Email = string.Create(CultureInfo.InvariantCulture, $"fo-test-{user.Vid}@example.invalid");
        }

        await database.SaveChangesAsync(cancellationToken);
        await database.Users.Where(user => user.Vid == SuperadminPilotVid)
            .ExecuteUpdateAsync(set => set.SetProperty(user => user.IsSuperadmin, true), cancellationToken);
    }

    /// <summary>The grants and the mails of these tests, taken back; the super administrator is one no more.</summary>
    private async Task CleanReviewersAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var vids = new[] { PilotVid, OtherPilotVid, ValidatorVid, SecondValidatorVid, CoordinatorVid, SuperadminPilotVid };

        await database.UserGrants.Where(grant => grant.Vid != null && vids.Contains(grant.Vid.Value)).ExecuteDeleteAsync(cancellationToken);
        await database.Notifications.Where(row => vids.Contains(row.Vid)).ExecuteDeleteAsync(cancellationToken);
        await database.Users.Where(user => user.Vid == SuperadminPilotVid)
            .ExecuteUpdateAsync(set => set.SetProperty(user => user.IsSuperadmin, false), cancellationToken);
    }
}
