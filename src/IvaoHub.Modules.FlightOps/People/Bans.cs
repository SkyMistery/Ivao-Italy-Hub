using System.Globalization;
using FluentValidation;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Notifications;
using IvaoHub.Core.Services;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Review;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.FlightOps.People;

/// <summary>A ban as the list and the form show it: who, on which tour or on all, from when to when, why, and whether it holds now.</summary>
public sealed record BanDto(
    long Id,
    MemberDto Pilot,
    long? TourId,
    Localized<string>? TourTitle,
    DateTime StartsAt,
    DateTime? EndsAt,
    string Reason,
    bool Active,
    DateTime CreatedAt,
    MemberDto? CreatedBy,
    DateTime RowVersion);

/// <summary>
/// What the staff writes on a ban. <c>TourId</c> null is every tour, <c>EndsAt</c> null is for good. Lifting a ban early is
/// moving its end: a ban is never deleted, it is part of the pilot's record (design M2 §10.1).
/// </summary>
public sealed record BanWriteDto(int Vid, long? TourId, DateTime StartsAt, DateTime? EndsAt, string? Reason, DateTime RowVersion);

public sealed class BanWriteDtoValidator : AbstractValidator<BanWriteDto>
{
    public BanWriteDtoValidator()
    {
        RuleFor(ban => ban.Vid).GreaterThan(0).WithMessage("errors.required");
        RuleFor(ban => ban.StartsAt).NotEmpty().WithMessage("errors.required");
        RuleFor(ban => ban.EndsAt)
            .Must((ban, end) => end is null || end > ban.StartsAt)
            .WithMessage("flightops:errors.banEndsBeforeStart");
        RuleFor(ban => ban.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason)).WithMessage("errors.required")
            .MaximumLength(PirepValidation.MaxTextLength).WithMessage("errors.text.tooLong");
    }
}

/// <summary>
/// The bans (design M2 §3.9, §8.7): the generic list and form at <c>/api/flightops/bans</c>, read with <c>Tours.ViewPilots</c> —
/// the pilot's page shows them — and written with <c>Tours.Ban</c> (FOC, FOAC, HQ, superadmin). A ban names a tour of the first
/// level or none: a ban on a container holds on its subtours (<see cref="Ban.Holds"/>). The pilot is told by mail, in their
/// language, with the reason and how long (<c>flightops.banned</c>) — when the ban is written, not when it is changed.
/// <para>What a ban does is T11's: no report on the tours it names while it holds; the reports already sent are validated as
/// usual.</para>
/// </summary>
public static class BanEndpoints
{
    public const string Pattern = "/api/flightops/bans";

    public static IEndpointRouteBuilder MapBanEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var clock = app.ServiceProvider.GetRequiredService<IClock>();

        app.MapCrud<Ban, BanDto, BanDto, BanWriteDto>(Pattern, options =>
        {
            options.PermissionArea = TourPermissions.Area;
            options.Name = "FlightOpsBans";
            options.ReadPolicy = TourPermissions.ViewPilots;
            options.WritePolicy = TourPermissions.Ban;
            options.ContextType = typeof(FlightOpsDbContext);
            options.AllowDelete = false;

            options.DefaultOrder = ban => ban.StartsAt;
            options.Sortable.Add(nameof(Ban.StartsAt));
            options.Sortable.Add(nameof(Ban.Vid));
            options.Filterable.Add(nameof(Ban.Vid));
            options.Filterable.Add(nameof(Ban.TourId));
            options.SearchFields.Add(ban => ban.Reason);

            options.ToList = ban => Row(ban, null, new Dictionary<int, string>(), clock.UtcNow);
            options.ToListPage = PageAsync;
            options.ToDetail = ban => Row(ban, null, new Dictionary<int, string>(), clock.UtcNow);
            options.Apply = (payload, ban) =>
            {
                ban.Vid = payload.Vid;
                ban.TourId = payload.TourId;
                ban.StartsAt = payload.StartsAt;
                ban.EndsAt = payload.EndsAt;
                ban.Reason = payload.Reason?.Trim() ?? string.Empty;
                ban.RowVersion = payload.RowVersion;
            };
            options.BeforeSave = CheckTourAsync;
            options.AfterSave = TellThePilotAsync;
        });

        return app;
    }

    /// <summary>A tour of the first level, not a template: a subtour is covered by its container's ban.</summary>
    private static async Task<IReadOnlyDictionary<string, string[]>?> CheckTourAsync(Ban ban, CrudSaving saving)
    {
        if (ban.TourId is not { } tourId)
        {
            return null;
        }

        var exists = await CrudSource.BackOffice<Tour>(saving.Database)
            .AnyAsync(tour => tour.Id == tourId && !tour.IsTemplate && tour.ParentTourId == null, saving.CancellationToken);
        return exists ? null : new Dictionary<string, string[]>(StringComparer.Ordinal) { ["tourId"] = ["flightops:errors.banTour"] };
    }

    /// <summary>The mail to the pilot (§3.9), once, when the ban is written.</summary>
    private static async Task TellThePilotAsync(Ban ban, CrudSaving saving)
    {
        if (!saving.IsNew)
        {
            return;
        }

        var services = saving.Services;
        var options = services.GetRequiredService<IOptions<DivisionOptions>>().Value;
        var catalog = services.GetRequiredService<LocaleCatalog>();
        var hub = services.GetRequiredService<HubDbContext>();
        var locale = await hub.Users.AsNoTracking()
            .Where(user => user.Vid == ban.Vid)
            .Select(user => user.Locale)
            .FirstOrDefaultAsync(saving.CancellationToken) ?? options.DefaultLocale;

        var tour = ban.TourId is { } tourId
            ? (await CrudSource.BackOffice<Tour>(saving.Database).AsNoTracking().FirstAsync(row => row.Id == tourId, saving.CancellationToken))
                .Title.Resolve(locale, options.DefaultLocale) ?? string.Empty
            : catalog.Resolve(locale, "flightops:mail.flightops.allTours");

        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tour"] = tour,
            ["from"] = ban.StartsAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC",
            ["until"] = ban.EndsAt is { } end
                ? end.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC"
                : catalog.Resolve(locale, "flightops:mail.flightops.forGood"),
            ["reason"] = ban.Reason,
            ["url"] = $"https://{options.Domain}/tours",
        };

        await services.GetRequiredService<INotificationService>().QueueAsync(
            new NotificationIntent(FlightOpsNotifications.Banned, [NotificationRecipient.Member(ban.Vid)], data),
            saving.CancellationToken);
    }

    /// <summary>The rows of one page: the tours' titles and the names of the pilots and of who wrote the ban.</summary>
    private static Task<IReadOnlyList<BanDto>> PageAsync(IReadOnlyList<Ban> bans, IServiceProvider services, CancellationToken cancellationToken) =>
        RowsAsync(
            bans,
            services.GetRequiredService<FlightOpsDbContext>(),
            services.GetRequiredService<PirepReview>(),
            services.GetRequiredService<IClock>().UtcNow,
            cancellationToken);

    /// <summary>Bans as the list shows them; the pilot's page shows theirs the same way.</summary>
    public static async Task<IReadOnlyList<BanDto>> RowsAsync(
        IReadOnlyList<Ban> bans,
        FlightOpsDbContext database,
        PirepReview reviews,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bans);
        ArgumentNullException.ThrowIfNull(reviews);

        var tourIds = bans.Select(ban => ban.TourId).OfType<long>().Distinct().ToList();
        var titles = await CrudSource.BackOffice<Tour>(database).AsNoTracking()
            .Where(tour => tourIds.Contains(tour.Id))
            .ToDictionaryAsync(tour => tour.Id, tour => tour.Title, cancellationToken);
        var names = await reviews.NamesAsync(bans.SelectMany(ban => new int?[] { ban.Vid, ban.CreatedBy }), cancellationToken);

        return [.. bans.Select(ban => Row(ban, ban.TourId is { } id ? titles.GetValueOrDefault(id) : null, names, now))];
    }

    private static BanDto Row(Ban ban, Localized<string>? tourTitle, IReadOnlyDictionary<int, string> names, DateTime now) =>
        new(
            ban.Id,
            PirepReview.Member(ban.Vid, names)!,
            ban.TourId,
            tourTitle,
            ban.StartsAt,
            ban.EndsAt,
            ban.Reason,
            ban.StartsAt <= now && (ban.EndsAt is null || ban.EndsAt > now),
            ban.CreatedAt,
            PirepReview.Member(ban.CreatedBy > 0 ? ban.CreatedBy : null, names),
            ban.RowVersion);
}
