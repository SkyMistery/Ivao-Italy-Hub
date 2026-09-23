using System.Globalization;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Modules.FlightOps.Data;
using IvaoHub.Modules.FlightOps.Pireps;
using IvaoHub.Modules.FlightOps.Rules;
using IvaoHub.Modules.FlightOps.Tours;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IvaoHub.Modules.FlightOps.Threads;

/// <summary>
/// What the tours say about the objects a thread cites (note 2026-09-23-contestazioni-chiarimenti-segnalazioni §3.2): three
/// forms, each answered as the caller — <c>null</c> when it does not exist or is not theirs to see.
/// <list type="bullet">
/// <item><c>pirep:{id}</c> — a report not withdrawn: its pilot reads it on the tour's page, whoever may validate on the validation
/// page. Whoever decided it takes part (design M2 §3.10).</item>
/// <item><c>leg:{id}</c> — a leg still in a tour the public sees.</item>
/// <item><c>rule:{tourId}:{ruleId}</c> — a rule in force on that tour: a general rule holds on many, and the link needs the one.</item>
/// </list>
/// <para>The label is written here once, in every language of the division, for the resolver and for the dispute that opens a
/// thread without it: <c>tour · leg 3 LIRF → LIML (2026-09-20)</c>.</para>
/// </summary>
public sealed class FlightOpsReferences(
    FlightOpsDbContext database,
    PirepSubmission submission,
    EffectiveRules effectiveRules,
    IAuthorizationService authorization,
    IHttpContextAccessor http,
    LocaleCatalog catalog,
    IOptions<DivisionOptions> division,
    ICurrentUser currentUser) : IContactReferenceResolver
{
    public string SourceModule => FlightOpsModule.ModuleKey;

    public static string LegReference(long legId) => $"leg:{legId}";

    public static string RuleReference(long tourId, long ruleId) => $"rule:{tourId}:{ruleId}";

    public async Task<ContactReferenceTarget?> ResolveAsync(string sourceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceId);

        var parts = sourceId.Split(':');
        return parts switch
        {
            ["pirep", var id] when Number(id) is { } pirep => await PirepAsync(pirep, cancellationToken),
            ["leg", var id] when Number(id) is { } leg => await LegAsync(leg, cancellationToken),
            ["rule", var tour, var id] when Number(tour) is { } tourId && Number(id) is { } rule =>
                await RuleAsync(tourId, rule, cancellationToken),
            _ => null,
        };
    }

    /// <summary>How a report reads in a thread: its tour, its leg, the route and the day of the flight.</summary>
    public Localized<string> PirepLabel(Tour tour, Pirep pirep)
    {
        ArgumentNullException.ThrowIfNull(tour);
        ArgumentNullException.ThrowIfNull(pirep);

        var number = PirepSubmission.LegSnapshot(pirep).Number;
        var date = pirep.TakeoffAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return Label(locale => Words(
            locale,
            number is null ? "threads.pirepLabelOpen" : "threads.pirepLabel",
            ("tour", Title(tour, locale)),
            ("number", number?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            ("from", pirep.DepartureIcao),
            ("to", pirep.ArrivalIcao),
            ("date", date)));
    }

    /// <summary>A sentence of the module's language file with its <c>{{names}}</c> filled in, the way the browser fills them.</summary>
    public string Words(string locale, string key, params (string Name, string Value)[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var text = catalog.Resolve(locale, key);
        foreach (var (name, value) in values)
        {
            text = text.Replace("{{" + name + "}}", value, StringComparison.Ordinal);
        }

        return text;
    }

    private async Task<ContactReferenceTarget?> PirepAsync(long id, CancellationToken cancellationToken)
    {
        // Every report, whoever asks: which ones the caller sees is decided just below, as the pilot's pages and the validation
        // decide it.
        var pirep = await CrudSource.BackOffice<Pirep>(database).AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == id && row.Status != PirepStatus.Withdrawn, cancellationToken);
        if (pirep is null)
        {
            return null;
        }

        var tour = await CrudSource.BackOffice<Tour>(database).AsNoTracking().FirstAsync(row => row.Id == pirep.TourId, cancellationToken);
        string url;
        if (pirep.Vid == currentUser.Vid)
        {
            url = $"/tours/{tour.Slug ?? await ParentSlugAsync(tour, cancellationToken)}";
        }
        else if (http.HttpContext is { } context
            && (await authorization.AuthorizeAsync(context.User, TourPermissions.Validate)).Succeeded)
        {
            // Anybody who may validate one tour reads every report (§4.1), as the queue does.
            url = $"/staff/tours/review/{pirep.Id}";
        }
        else
        {
            return null;
        }

        return new ContactReferenceTarget(
            PirepLabel(tour, pirep),
            url,
            pirep.DecidedByVid is { } decider ? [decider] : []);
    }

    private async Task<ContactReferenceTarget?> LegAsync(long id, CancellationToken cancellationToken)
    {
        var leg = await database.Legs.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id && row.RetiredAt == null, cancellationToken);
        var pilot = leg is null ? null : await submission.TourAsync(leg.TourId, cancellationToken);
        if (leg is null || pilot?.Tour.Slug is not { } slug)
        {
            return null;
        }

        return new ContactReferenceTarget(
            Label(locale => Words(
                locale,
                "threads.legLabel",
                ("tour", Title(pilot.Tour, locale)),
                ("number", leg.Number.ToString(CultureInfo.InvariantCulture)),
                ("from", leg.DepartureIcao),
                ("to", leg.ArrivalIcao))),
            $"/tours/{slug}",
            []);
    }

    private async Task<ContactReferenceTarget?> RuleAsync(long tourId, long ruleId, CancellationToken cancellationToken)
    {
        var pilot = await submission.TourAsync(tourId, cancellationToken);
        if (pilot?.Tour.Slug is not { } slug)
        {
            return null;
        }

        var rule = (await effectiveRules.ForTourAsync(pilot.Tour, cancellationToken)).FirstOrDefault(entry => entry.Rule.Id == ruleId);
        if (rule is null)
        {
            return null;
        }

        return new ContactReferenceTarget(
            Label(locale => Words(
                locale,
                "threads.ruleLabel",
                ("tour", Title(pilot.Tour, locale)),
                ("code", rule.Rule.Code),
                ("title", rule.Rule.Title.Resolve(locale, division.Value.DefaultLocale) ?? string.Empty))),
            $"/tours/{slug}",
            []);
    }

    private Localized<string> Label(Func<string, string> text) =>
        new(division.Value.Locales.Select(locale => KeyValuePair.Create(locale, text(locale))));

    private string Title(Tour tour, string locale) => tour.Title.Resolve(locale, division.Value.DefaultLocale) ?? string.Empty;

    private async Task<string?> ParentSlugAsync(Tour tour, CancellationToken cancellationToken) =>
        tour.ParentTourId is { } parent
            ? await CrudSource.BackOffice<Tour>(database).Where(row => row.Id == parent).Select(row => row.Slug).FirstOrDefaultAsync(cancellationToken)
            : null;

    private static long? Number(string text) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null;
}
