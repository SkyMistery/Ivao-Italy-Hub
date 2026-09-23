using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Atc;

/// <summary>
/// Wires the archive of ATC sessions, when the division has one: <c>division.json → atcData.source</c>. The archive is named
/// here and in its own class, and nowhere else in the hub; a module asks <see cref="IAtcActivitySource"/>.
/// </summary>
public static class AtcServiceCollectionExtensions
{
    /// <summary>The connection string of the archive, in a file under <c>secrets/</c> (<c>ConnectionStrings:AtcData</c>).</summary>
    public const string ConnectionStringName = "AtcData";

    public static IServiceCollection AddAtcActivity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Registered whatever the division says: the connection is read only when a context is built, and none is built
        // unless the source below asks for one.
        services.AddSharedViewContext<VipiShareDbContext>(ConnectionStringName);
        services.AddScoped<VipiAtcActivitySource>();

        services.AddScoped<IAtcActivitySource>(provider =>
            provider.GetRequiredService<IOptions<DivisionOptions>>().Value.AtcData?.Source == AtcDataOptions.Vipi
                ? provider.GetRequiredService<VipiAtcActivitySource>()
                : new UnavailableAtcActivitySource());

        return services;
    }
}

/// <summary>Where the division's ATC sessions are read from, as <c>division.json → atcData</c> writes it.</summary>
public sealed class AtcDataOptions
{
    /// <summary>No archive: the controllers a pilot contacted are «not available» (the default, and a fork's).</summary>
    public const string None = "none";

    /// <summary>vIPI's view <c>v_share_atc_sessions</c>, over <c>ConnectionStrings:AtcData</c>.</summary>
    public const string Vipi = "vipi";

    public static readonly IReadOnlyList<string> KnownSources = [None, Vipi];

    public string Source { get; init; } = None;
}
