using IvaoHub.Core.Jobs;
using Serilog.Core;
using Serilog.Events;

namespace IvaoHub.Web;

/// <summary>
/// Keeps the token of the scheduled task's address out of the hub's log (note 2026-10-09-i-job-che-recuperano, Carmine on
/// #239). A panel that can only fetch an address calls <c>GET /api/jobs/run?token=…</c>, and ASP.NET Core writes the query
/// string of a request whole in its own lines ("Request starting …", "Request finished …") whenever their level lets them
/// through: in Development, or on an installation that lowers <c>Microsoft.AspNetCore</c> to look at its requests. The line
/// stays, and only the token's value goes (<see cref="JobRunEndpoints.MaskToken"/>). Serilog's own line of a request writes
/// the path, without the query string.
/// </summary>
internal sealed class JobTokenLogMask : ILogEventEnricher
{
    /// <summary>The property ASP.NET Core's lines of a request carry the query string in.</summary>
    public const string QueryStringProperty = "QueryString";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (logEvent.Properties.TryGetValue(QueryStringProperty, out var value)
            && value is ScalarValue { Value: string query }
            && JobRunEndpoints.MaskToken(query) is { } masked)
        {
            logEvent.AddOrUpdateProperty(new LogEventProperty(QueryStringProperty, new ScalarValue(masked)));
        }
    }
}
