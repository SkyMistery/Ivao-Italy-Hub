using System.Diagnostics;
using System.Runtime;
using IvaoHub.Core.Services;

namespace IvaoHub.Web;

/// <summary>
/// Times every request and queues its line for <c>diagnostics/requests-YYYYMMDD.txt</c> (<see cref="RequestTimingsLog"/>,
/// note 2026-10-18-i-tempi-delle-richieste).
/// </summary>
internal static class RequestTimingsMiddleware
{
    private static readonly HashSet<string> KnownMethods =
        new(["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"], StringComparer.Ordinal);

    /// <summary>
    /// Right after the counter of <c>starts.txt</c>, before everything else, so that the time of every middleware counts. The
    /// route is read after the response, when routing has chosen the endpoint; what reached none is written as such.
    /// </summary>
    public static IApplicationBuilder UseRequestTimings(this WebApplication app, StartupTimings timings)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(timings);

        var log = app.Services.GetRequiredService<RequestTimingsLog>();
        if (!log.Enabled)
        {
            return app;
        }

        long requests = 0;
        var pid = Environment.ProcessId;

        return app.Use(async (context, next) =>
        {
            var number = Interlocked.Increment(ref requests);
            var uptime = timings.Elapsed;
            var started = Stopwatch.GetTimestamp();
            var jit = JitInfo.GetCompilationTime();
            var jitMethods = JitInfo.GetCompiledMethodCount();
            var allocated = GC.GetTotalAllocatedBytes();
            long firstByte = -1;

            var timing = new RequestTiming();
            RequestTiming.Current = timing;
            context.Response.OnStarting(() =>
            {
                Interlocked.CompareExchange(ref firstByte, Stopwatch.GetTimestamp(), -1);
                return Task.CompletedTask;
            });

            var failed = false;
            try
            {
                await next();
            }
            catch
            {
                failed = true;
                throw;
            }
            finally
            {
                RequestTiming.Current = null;
                try
                {
                    var first = Interlocked.Read(ref firstByte);
                    log.TryWrite(new RequestTimingsLine(
                        DateTime.UtcNow,
                        pid,
                        number,
                        uptime,
                        KnownMethods.Contains(context.Request.Method) ? context.Request.Method : "OTHER",
                        failed && !context.Response.HasStarted ? StatusCodes.Status500InternalServerError : context.Response.StatusCode,
                        Route(context),
                        Stopwatch.GetElapsedTime(started),
                        first < 0 ? null : Stopwatch.GetElapsedTime(started, first),
                        timing.Commands,
                        timing.CommandTime,
                        timing.Opens,
                        timing.OpenTime,
                        timing.Calls,
                        timing.CallTime,
                        JitInfo.GetCompilationTime() - jit,
                        JitInfo.GetCompiledMethodCount() - jitMethods,
                        (GC.GetTotalAllocatedBytes() - allocated) / 1024));
                }
#pragma warning disable CA1031 // The line of a request is lost, never the request.
                catch (Exception)
#pragma warning restore CA1031
                {
                }
            }
        });
    }

    /// <summary>The pattern the endpoint declares, never the path that was asked.</summary>
    private static string Route(HttpContext context) => context.GetEndpoint() switch
    {
        RouteEndpoint { RoutePattern.RawText: { Length: > 0 } pattern } => pattern.StartsWith('/') ? pattern : "/" + pattern,
        null => RequestTimingsLine.NoEndpoint(context.Request.Path.Value),
        _ => "(endpoint without a route)",
    };
}
