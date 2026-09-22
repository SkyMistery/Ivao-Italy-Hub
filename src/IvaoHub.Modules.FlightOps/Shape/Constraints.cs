using System.Text.Json.Nodes;
using IvaoHub.Core.Airspace;
using IvaoHub.Core.Division;
using IvaoHub.Core.Ivao;
using IvaoHub.Modules.FlightOps.Legs;

namespace IvaoHub.Modules.FlightOps.Shape;

/// <summary>
/// A filter or a sequence rule of an <c>Open</c> tour (design M2 §2.6.1), <c>fo_tour_constraints</c>: its kind, and the
/// parameters the kind takes (<see cref="OpenCatalog"/>). One row per kind, but one per airport of
/// <see cref="TourConstraintKind.MinFlightsAt"/>; the kind never changes once written (note <c>2026-09-22-il-tour-open</c>).
/// </summary>
[Audited]
[PermissionArea(TourPermissions.Area)]
public sealed class TourConstraint : ITourChild, IAuditable
{
    public long Id { get; set; }

    public long TourId { get; set; }

    public TourConstraintKind Kind { get; set; }

    /// <summary>The parameters as a JSON object holding the kind's fields and nothing else.</summary>
    public string ParametersJson { get; set; } = "{}";

    public Department OwnerDepartment { get; set; }

    public int OwnerDepartmentMask { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    /// <summary>Whether the tour is a template, read before the permission is asked (as <see cref="CallsignRule.OnTemplate"/>).</summary>
    public bool OnTemplate { get; set; }
}

/// <summary>
/// Whether what the parameters of a goal or a constraint name exists (note <c>2026-09-22-il-tour-open</c> §3): airports and
/// countries the hub knows, regions with an outline. The shape of the values is <see cref="OpenCatalog"/>'s.
/// </summary>
public sealed class OpenParameterCheck(IAirportDirectory airports, IFirLocator firs)
{
    public async Task<IReadOnlyList<ShapeProblem>> UnknownAsync(
        IReadOnlyList<ParameterField> fields,
        JsonObject parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(parameters);

        var problems = new List<ShapeProblem>();

        foreach (var field in fields)
        {
            var codes = OpenCatalog.Codes(parameters, field.Name);
            if (codes.Count == 0)
            {
                continue;
            }

            var (known, key) = field.Type switch
            {
                ParameterType.Airport or ParameterType.Airports => (
                    (await airports.FindAsync(codes, cancellationToken)).Keys.ToHashSet(StringComparer.Ordinal),
                    "flightops:errors.airportUnknown"),
                ParameterType.Countries =>
                    (await airports.KnownCountriesAsync(codes, cancellationToken), "flightops:errors.countryUnknown"),
                ParameterType.Firs => (await firs.KnownAsync(codes, cancellationToken), "flightops:errors.firUnknown"),
                _ => ((IReadOnlySet<string>?)null, string.Empty),
            };

            if (known is not null && !codes.All(known.Contains))
            {
                problems.Add(new(field.Name, key));
            }
        }

        return problems;
    }

    /// <summary>The problems as the form reads them, each under <c>{prefix}.{field}</c>; null when there are none.</summary>
    public static IReadOnlyDictionary<string, string[]>? Refusal(string prefix, IEnumerable<ShapeProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        var errors = problems
            .GroupBy(problem => $"{prefix}.{problem.Field}", StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(problem => problem.Key).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        return errors.Count == 0 ? null : errors;
    }
}
