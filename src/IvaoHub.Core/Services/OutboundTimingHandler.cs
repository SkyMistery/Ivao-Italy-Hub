using System.Diagnostics;

namespace IvaoHub.Core.Services;

/// <summary>
/// Adds the time of every outbound HTTP call (IVAO, the weather, the FIR outlines) to <see cref="RequestTiming.Here"/>
/// (note 2026-10-18-i-tempi-delle-richieste). The outermost handler of every client of the factory, so its time is what
/// the caller waited: retries and the waits between them included. It reads neither the address nor the answer.
/// </summary>
public sealed class OutboundTimingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var here = RequestTiming.Here;
        var started = Stopwatch.GetTimestamp();
        try
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            here.Called(Stopwatch.GetElapsedTime(started));
        }
    }
}
