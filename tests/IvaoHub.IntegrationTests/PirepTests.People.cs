using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Modules.FlightOps;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.People;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using static IvaoHub.IntegrationTests.FoTestAirports;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// The people of the tours through the real host (M2, T15): the completion that signals the award in the save that accepts the
/// report and is never taken back — a sequence, and a container through a subtour flown by distance —; «add a validator», who
/// takes at once on their tour, and on a container's subtours, and loses it all when they leave the staff; the ban that stops
/// the reports and not the validation of the ones sent; the pilot's page.
/// </summary>
public sealed partial class PirepTests
{
    // T15 takes 89 and 90: two members of the staff of another department, who are what «add a validator» may enable.
    private const int StaffValidatorVid = 780089;
    private const int LeavingValidatorVid = 780090;

    private readonly List<long> _awards = [];

    /// <summary>
    /// The core of the "done when" of T15 without the browser: the report that finishes a sequence completes it and points the pilot
    /// out for the tour's award in the same save; one leg before, nothing. A leg added afterwards, and the decision reopened and
    /// turned into a rejection, leave the completion and the signal where they are.
    /// </summary>
    [Fact]
    public async Task TheReportThatFinishesATourCompletesItAndSignalsTheAwardForGood()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var award = await AwardAsync(token);
        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token, awardId: award);
        var (dangerous, _, _) = await RuleWithErrorsAsync(coordinator, tourId, yearlyMax: 3, token);
        await GrantValidateAsync(ValidatorVid, tourId, token);
        using var validator = await SignedInAsync(ValidatorVid, token);

        var first = await AcceptedAsync(pilot, validator, tourId, legs[0], Rome, Milan, DateTime.UtcNow.AddDays(-1), token);
        var enrolment = await EnrolmentAsync(tourId, PilotVid, token);
        Assert.Null(enrolment.CompletedAt);
        Assert.Empty(await SignalsAsync(enrolment.Id, token));

        var second = await AcceptedAsync(pilot, validator, tourId, legs[1], Milan, London, DateTime.UtcNow.AddDays(-1).AddHours(3), token);
        enrolment = await EnrolmentAsync(tourId, PilotVid, token);
        Assert.NotNull(enrolment.CompletedAt);
        var signal = Assert.Single(await SignalsAsync(enrolment.Id, token));
        Assert.Equal(PilotVid, signal.Vid);
        Assert.Equal(award, signal.AwardId);
        Assert.Equal(AwardSignalStatus.Pending, signal.Status);
        Assert.Contains("fo-test-pirep-", signal.Reason, StringComparison.Ordinal);
        Assert.True(Assert.Single(await EventsAsync(first, token), row => row.ToStatus == PirepStatus.Accepted).At <= enrolment.CompletedAt);

        var mine = await OkAsync(await pilot.GetAsync($"{Reports(tourId)}/mine", token), token);
        Assert.True(mine.GetProperty("finished").GetBoolean());

        // A leg added afterwards: the tour is no longer finished for the rules, and stays completed.
        await OkAsync(await coordinator.PostAsJsonAsync(LegsUri(tourId), Leg(London, Rome), token), token);

        // The decision reopened and turned into a rejection: still completed, and the signal still the one written.
        var page = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{second}", token), token);
        page = await StepAsync(coordinator, second, "reopen", new ReviewReopenDto("fo-test the SID", RowVersion(page)), token);
        await StepAsync(coordinator, second, "decide", Decision(PirepStatus.Rejected, [dangerous], RowVersion(page)), token);

        var after = await EnrolmentAsync(tourId, PilotVid, token);
        Assert.Equal(enrolment.CompletedAt, after.CompletedAt);
        Assert.Equal(signal.Id, Assert.Single(await SignalsAsync(enrolment.Id, token)).Id);
    }

    /// <summary>
    /// A container completes through its subtours (§2.7): one subtour flown by distance completes it, signals nothing of its own,
    /// and completes the container, which signals its award. The validator is enabled on the container and takes the subtour's
    /// report — the subtour does not appear where validators are enabled (Carmine, 23 September 2026).
    /// </summary>
    [Fact]
    public async Task ASubtourFlownByDistanceCompletesItsContainerWhichSignalsItsAward()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var award = await AwardAsync(token);
        var containerId = await TourAsync(coordinator, TourKind.Container, parent: null, awardId: award, requiredNm: null, requiredSubtours: 1, token);
        var (distanceId, distanceLegs) = await SubtourAsync(coordinator, containerId, TourKind.Distance, requiredNm: 100, [(Rome, Milan), (Milan, London)], token);
        await SubtourAsync(coordinator, containerId, TourKind.Sequential, requiredNm: null, [(Milan, London)], token);
        await ReadyAsync(coordinator, containerId, token);

        await GrantValidateAsync(ValidatorVid, containerId, token);
        using var validator = await SignedInAsync(ValidatorVid, token);

        await AcceptedAsync(pilot, validator, distanceId, distanceLegs[0], Rome, Milan, DateTime.UtcNow.AddDays(-1), token);

        var subtour = await EnrolmentAsync(distanceId, PilotVid, token);
        var container = await EnrolmentAsync(containerId, PilotVid, token);
        Assert.NotNull(subtour.CompletedAt);
        Assert.Empty(await SignalsAsync(subtour.Id, token));
        Assert.NotNull(container.CompletedAt);
        var signal = Assert.Single(await SignalsAsync(container.Id, token));
        Assert.Equal(award, signal.AwardId);
    }

    /// <summary>
    /// «Add a validator» (§7.2, §8.7): only a member of the staff, on a tour of the first level or on every tour, by whoever
    /// holds <c>Tours.ManageValidators</c>. The validator takes at once on that tour and not on another, reads
    /// the pilot's page, and appears in the statistics with what they decided; removed, they lose both grants.
    /// </summary>
    [Fact]
    public async Task AValidatorAddedTakesAtOnceOnTheirTourAndIsCountedAndRemoved()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (otherId, otherLegs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);

        // Whoever may not manage validators may not add one; a pilot is not staff; a subtour is its container's.
        using (var forbidden = await pilot.PostAsJsonAsync(ValidatorEndpoints.Pattern, new ValidatorWriteDto(StaffValidatorVid, tourId), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        await RefusedAsync(coordinator, ValidatorEndpoints.Pattern, new ValidatorWriteDto(OtherPilotVid, tourId), "vid", "errors.grant.notStaff", token);
        var containerId = await TourAsync(coordinator, TourKind.Container, parent: null, awardId: null, requiredNm: null, requiredSubtours: 1, token);
        var subtourId = await TourAsync(coordinator, TourKind.Free, parent: containerId, awardId: null, requiredNm: null, requiredSubtours: null, token);
        await RefusedAsync(coordinator, ValidatorEndpoints.Pattern, new ValidatorWriteDto(StaffValidatorVid, subtourId), "tourId", "flightops:errors.validatorTour", token);

        using (var added = await coordinator.PostAsJsonAsync(ValidatorEndpoints.Pattern, new ValidatorWriteDto(StaffValidatorVid, tourId), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        }

        // At once, from their next sign-in — a grant written signs its holder out, so the cookie never outlives it (M0): the
        // report on their tour they take, the one on the other tour they do not.
        using var staff = await SignedInAsync(StaffValidatorVid, token);
        var theirs = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1))), token));
        var others = Id(await CreatedAsync(pilot, Reports(otherId), Payload(otherLegs[0], _flights.Add(PilotVid, "XAA300", Rome, Milan, DateTime.UtcNow.AddDays(-1).AddHours(5))), token));
        var page = await TakeAsync(staff, theirs, token);
        await StepAsync(staff, theirs, "decide", Decision(PirepStatus.Accepted, [], RowVersion(page)), token);
        var other = await OkAsync(await staff.GetAsync($"{ReviewEndpoints.Pattern}/{others}", token), token);
        using (var refused = await staff.PostAsJsonAsync($"{ReviewEndpoints.Pattern}/{others}/take", new ReviewStepDto(RowVersion(other)), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // With it, the pilot's page and the statistics (answer 17).
        await OkAsync(await staff.GetAsync($"{PilotEndpoints.Pattern}/{PilotVid}", token), token);
        var statistics = await OkAsync(await staff.GetAsync(ValidatorEndpoints.Pattern, token), token);
        var row = statistics.GetProperty("validators").EnumerateArray().Single(entry => Member(entry) == StaffValidatorVid);
        Assert.Equal([tourId], row.GetProperty("tourIds").EnumerateArray().Select(id => id.GetInt64()));
        Assert.False(row.GetProperty("allTours").GetBoolean());
        Assert.Equal(1, row.GetProperty("accepted").GetInt32());
        var perTour = statistics.GetProperty("tours").EnumerateArray().Single(entry => entry.GetProperty("tourId").GetInt64() == tourId);
        Assert.Equal(1, perTour.GetProperty("counts").EnumerateArray().Single(entry => entry.GetProperty("vid").GetInt32() == StaffValidatorVid).GetProperty("accepted").GetInt32());

        // Removed from the tour: both grants go, and with them the page.
        using (var removed = await coordinator.DeleteAsync($"{ValidatorEndpoints.Pattern}/{StaffValidatorVid}?tourId={tourId}", token))
        {
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        using (var gone = await coordinator.DeleteAsync($"{ValidatorEndpoints.Pattern}/{StaffValidatorVid}?tourId={tourId}", token))
        {
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        }

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            Assert.False(await hub.UserGrants.AnyAsync(grant => grant.Vid == StaffValidatorVid, token));
        }

        using (var signedOut = await staff.GetAsync($"{PilotEndpoints.Pattern}/{PilotVid}", token))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
        }

        using var again = await SignedInAsync(StaffValidatorVid, token);
        using (var closed = await again.GetAsync($"{PilotEndpoints.Pattern}/{PilotVid}", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, closed.StatusCode);
        }
    }

    /// <summary>
    /// A validator enabled on a tour and on every tour who leaves the staff (§7.2): the sync of their login suspends the grants —
    /// the one with a scope as well —, which stay, as the statistics say, and they take nothing any more.
    /// </summary>
    [Fact]
    public async Task AValidatorWhoLeavesTheStaffLosesTheGrantAtTheSync()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);

        // On the tour, and on every tour: a grant with a scope and one without (the test T3 left to this phase).
        foreach (var scope in new long?[] { tourId, null })
        {
            using var added = await coordinator.PostAsJsonAsync(ValidatorEndpoints.Pattern, new ValidatorWriteDto(LeavingValidatorVid, scope), token);
            Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        }

        var id = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], _flights.Add(PilotVid, "XAA100", Rome, Milan, DateTime.UtcNow.AddDays(-1))), token));

        // IVAO no longer lists a position of this division for them.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var sync = scope.ServiceProvider.GetRequiredService<UserSyncService>();
            await sync.UpsertAsync(
                new IvaoUserProfile(LeavingValidatorVid, "Test", "Pirep", null, "IT", "IT", null, 4, null, null, "en", false, false, []),
                token);
        }

        using var left = await SignedInAsync(LeavingValidatorVid, token);
        var page = await OkAsync(await coordinator.GetAsync($"{ReviewEndpoints.Pattern}/{id}", token), token);
        using (var refused = await left.PostAsJsonAsync($"{ReviewEndpoints.Pattern}/{id}/take", new ReviewStepDto(RowVersion(page)), token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        var statistics = await OkAsync(await coordinator.GetAsync(ValidatorEndpoints.Pattern, token), token);
        var row = statistics.GetProperty("validators").EnumerateArray().Single(entry => Member(entry) == LeavingValidatorVid);
        Assert.True(row.GetProperty("allTours").GetBoolean());
        Assert.Equal([tourId], row.GetProperty("tourIds").EnumerateArray().Select(id => id.GetInt64()));
        Assert.True(row.GetProperty("suspended").GetBoolean());

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
            var grants = await hub.UserGrants.AsNoTracking().Where(grant => grant.Vid == LeavingValidatorVid).ToListAsync(token);
            Assert.Equal(3, grants.Count);
            Assert.All(grants, grant => Assert.NotNull(grant.SuspendedAt));
        }
    }

    /// <summary>
    /// A ban (§3.9): written by the coordinator with <c>Tours.Ban</c> — not by a validator — on a tour of the first level; the pilot
    /// hears it by mail with the reason; their next report is refused, and the one already sent is validated as usual. The pilot's
    /// page shows the ban, the flights with who decided them and the errors confirmed by category and by error.
    /// </summary>
    [Fact]
    public async Task ABanStopsTheReportsNotTheValidationAndThePilotsPageShowsIt()
    {
        var token = TestContext.Current.CancellationToken;
        using var coordinator = await SignedInAsync(CoordinatorVid, token);
        using var pilot = await SignedInAsync(PilotVid, token);

        var (tourId, legs) = await ReadyTourAsync(coordinator, dailyLimit: 5, token);
        var (dangerous, _, _) = await RuleWithErrorsAsync(coordinator, tourId, yearlyMax: 3, token);
        await GrantValidateAsync(ValidatorVid, tourId, token);
        using var validator = await SignedInAsync(ValidatorVid, token);

        var rejected = await RejectedAsync(pilot, validator, tourId, legs[0], dangerous, token);
        var pending = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legs[0], _flights.Add(PilotVid, "XAA101", Rome, Milan, DateTime.UtcNow.AddHours(-20))), token));

        var ban = new BanWriteDto(PilotVid, tourId, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddDays(30), "fo-test-pirep three dangerous errors", default);
        using (var forbidden = await validator.PostAsJsonAsync(BanEndpoints.Pattern, ban, token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        var containerId = await TourAsync(coordinator, TourKind.Container, parent: null, awardId: null, requiredNm: null, requiredSubtours: 1, token);
        var subtourId = await TourAsync(coordinator, TourKind.Free, parent: containerId, awardId: null, requiredNm: null, requiredSubtours: null, token);
        await RefusedAsync(coordinator, BanEndpoints.Pattern, ban with { TourId = subtourId }, "tourId", "flightops:errors.banTour", token);
        await RefusedAsync(coordinator, BanEndpoints.Pattern, ban with { EndsAt = ban.StartsAt.AddDays(-1) }, "endsAt", "flightops:errors.banEndsBeforeStart", token);

        var written = await CreatedAsync(coordinator, BanEndpoints.Pattern, ban, token);
        Assert.True(written.GetProperty("active").GetBoolean());
        var mail = await MailAsync(PilotVid, FlightOpsNotifications.Banned, token);
        Assert.Contains("fo-test-pirep three dangerous errors", mail.DataJson, StringComparison.Ordinal);

        // No report on the tour while it holds.
        var flown = _flights.Add(PilotVid, "XAA200", Milan, London, DateTime.UtcNow.AddHours(-10));
        await RefusedAsync(pilot, Reports(tourId), Payload(legs[1], flown), "tour", "flightops:errors.reportBanned", token);

        // The one already sent is validated as usual.
        var page = await TakeAsync(validator, pending, token);
        page = await StepAsync(validator, pending, "decide", Decision(PirepStatus.Accepted, [], RowVersion(page)), token);
        Assert.Equal("Accepted", page.GetProperty("status").GetString());

        // The pilot's page: not for the pilot; for the coordinator, everything, and the ban among it.
        using (var own = await pilot.GetAsync($"{PilotEndpoints.Pattern}/{PilotVid}", token))
        {
            Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        }

        var year = DateTime.UtcNow.AddHours(-20).Year;
        var profile = await OkAsync(await coordinator.GetAsync($"{PilotEndpoints.Pattern}/{PilotVid}?year={year}", token), token);
        Assert.True(profile.GetProperty("canBan").GetBoolean());
        var flights = profile.GetProperty("flights").EnumerateArray().Where(entry => entry.GetProperty("tourId").GetInt64() == tourId).ToList();
        Assert.Equal([pending, rejected], flights.Select(entry => entry.GetProperty("pirepId").GetInt64()));
        Assert.All(flights, entry => Assert.Equal(ValidatorVid, entry.GetProperty("decidedBy").GetProperty("vid").GetInt32()));
        var error = profile.GetProperty("errors").EnumerateArray().Single(entry => entry.GetProperty("errorId").GetInt64() == dangerous);
        Assert.Equal(1, error.GetProperty("ever").GetInt32());
        Assert.Equal("fo-test dangerous", error.GetProperty("name").GetProperty("en").GetString());
        Assert.True(profile.GetProperty("categories").EnumerateArray().Single(entry => entry.GetProperty("category").GetString() == "Dangerous").GetProperty("ever").GetInt32() >= 1);
        var shown = profile.GetProperty("bans").EnumerateArray().Single(entry => Id(entry) == Id(written));
        Assert.True(shown.GetProperty("active").GetBoolean());
        var standing = profile.GetProperty("tours").EnumerateArray().Single(entry => entry.GetProperty("tourId").GetInt64() == tourId);
        Assert.Equal(1, standing.GetProperty("done").GetInt32());
        Assert.Equal(2, standing.GetProperty("target").GetInt32());
        Assert.Equal("Legs", standing.GetProperty("unit").GetString());

        using (var nobody = await coordinator.GetAsync($"{PilotEndpoints.Pattern}/799999", token))
        {
            Assert.Equal(HttpStatusCode.NotFound, nobody.StatusCode);
        }
    }

    /// <summary>A report of the pilot on a leg, taken and accepted by the validator with no error; its id.</summary>
    private async Task<long> AcceptedAsync(
        HttpClient pilot,
        HttpClient validator,
        long tourId,
        long legId,
        string from,
        string to,
        DateTime takeoff,
        CancellationToken cancellationToken)
    {
        var flown = _flights.Add(PilotVid, "XAA100", from, to, takeoff);
        var id = Id(await CreatedAsync(pilot, Reports(tourId), Payload(legId, flown), cancellationToken));
        var page = await TakeAsync(validator, id, cancellationToken);
        await StepAsync(validator, id, "decide", Decision(PirepStatus.Accepted, [], RowVersion(page)), cancellationToken);
        return id;
    }

    /// <summary>A tour of this class, released four days ago, not yet ready.</summary>
    private async Task<long> TourAsync(
        HttpClient coordinator,
        TourKind kind,
        long? parent,
        long? awardId,
        int? requiredNm,
        int? requiredSubtours,
        CancellationToken cancellationToken)
    {
        var slug = $"fo-test-pirep-{Guid.NewGuid():N}"[..27];
        var tour = await CreatedAsync(
            coordinator,
            TourEndpoints.Pattern,
            new TourWriteDto(
                OwnerDepartment: Department.FOD,
                IsTemplate: false,
                Slug: slug,
                Kind: kind,
                Title: Text(slug),
                Summary: Text($"{slug} summary"),
                Briefing: null,
                CoverMediaId: null,
                BannerMediaId: null,
                ShowPreview: false,
                ReleaseAt: parent is null ? DateTime.UtcNow.AddDays(-4) : null,
                CloseAt: parent is null ? DateTime.UtcNow.AddDays(60) : null,
                ReportWindowDays: null,
                Progression: TourProgression.FlyAhead,
                HubRotationOrder: null,
                RequiresProcedures: false,
                DailyLegLimit: 5,
                MinPilotRating: 2,
                ReferenceAircraftIcao: null,
                RequiredNm: requiredNm,
                AllowedAircraft: null,
                AwardId: awardId,
                RowVersion: default,
                ParentTourId: parent,
                RequiredSubtours: requiredSubtours),
            cancellationToken);
        var id = Id(tour);

        // Subtours first when the class cleans up: a container is deleted after them.
        _tours.Insert(0, id);
        return id;
    }

    /// <summary>A subtour of the container with its legs, ready.</summary>
    private async Task<(long TourId, long[] Legs)> SubtourAsync(
        HttpClient coordinator,
        long containerId,
        TourKind kind,
        int? requiredNm,
        IReadOnlyList<(string From, string To)> route,
        CancellationToken cancellationToken)
    {
        var tourId = await TourAsync(coordinator, kind, containerId, awardId: null, requiredNm, requiredSubtours: null, cancellationToken);
        JsonElement grid = default;
        foreach (var (from, to) in route)
        {
            grid = await OkAsync(await coordinator.PostAsJsonAsync(LegsUri(tourId), Leg(from, to), cancellationToken), cancellationToken);
        }

        await ReadyAsync(coordinator, tourId, cancellationToken);
        return (tourId, [.. grid.GetProperty("legs").EnumerateArray().Select(Id)]);
    }

    private static async Task ReadyAsync(HttpClient coordinator, long tourId, CancellationToken cancellationToken) =>
        await OkAsync(
            await coordinator.PostAsJsonAsync($"{TourEndpoints.Pattern}/{tourId}/status", new TourStatusRequest(TourStatusAction.Ready), cancellationToken),
            cancellationToken);

    private async Task<Enrolment> EnrolmentAsync(long tourId, int vid, CancellationToken cancellationToken)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
        return await database.Enrolments.AsNoTracking().SingleAsync(row => row.TourId == tourId && row.Vid == vid, cancellationToken);
    }

    private async Task<List<AwardSignal>> SignalsAsync(long enrolmentId, CancellationToken cancellationToken)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var source = Enrolment.ReferenceOf(enrolmentId);
        return await hub.AwardSignals.AsNoTracking()
            .Where(row => row.SourceModule == FlightOpsModule.ModuleKey && row.SourceId == source)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<PirepEvent>> EventsAsync(long pirepId, CancellationToken cancellationToken)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlightOpsDbContext>();
        return await database.PirepEvents.AsNoTracking().Where(row => row.PirepId == pirepId).ToListAsync(cancellationToken);
    }

    private static int Member(JsonElement row) => row.GetProperty("member").GetProperty("vid").GetInt32();

    /// <summary>An award of the FOD for a tour to propose; taken back with the class.</summary>
    private async Task<long> AwardAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var award = new Core.Awards.Award { OwnerDepartment = Department.FOD, Name = Text("fo-test-pirep award") };
        hub.Awards.Add(award);
        await hub.SaveChangesAsync(cancellationToken);
        _awards.Add(award.Id);
        return award.Id;
    }

    /// <summary>The signals the completions of these tests wrote, taken back: they belong to the pilots of this class.</summary>
    private async Task CleanSignalsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var hub = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        var vids = new[] { PilotVid, OtherPilotVid };
        await hub.AwardSignals
            .Where(row => row.SourceModule == FlightOpsModule.ModuleKey && vids.Contains(row.Vid))
            .ExecuteDeleteAsync(cancellationToken);
        await hub.Awards.Where(award => _awards.Contains(award.Id)).ExecuteDeleteAsync(cancellationToken);
    }
}
