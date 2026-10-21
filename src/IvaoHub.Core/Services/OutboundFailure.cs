using Polly;

namespace IvaoHub.Core.Services;

/// <summary>
/// What an outbound call throws when the other side could not be reached, for the clients that read such a failure as "no
/// answer" (the weather, the FIR outlines). The transport's own exceptions, and the ones the standard resilience handler
/// throws in their place: its total timeout (<c>TimeoutRejectedException</c>) fires before the client's own, and an open
/// circuit (<c>BrokenCircuitException</c>) answers without calling at all (issue #325, item 1). Both are an
/// <see cref="ExecutionRejectedException"/>, as is the rate limiter's refusal.
/// <para>Not the caller's cancellation, which is also a <see cref="TaskCanceledException"/>: a client asks
/// <c>cancellationToken.ThrowIfCancellationRequested()</c> before it reads the failure as no answer.</para>
/// </summary>
public static class OutboundFailure
{
    public static bool CouldNotReach(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or ExecutionRejectedException;
}
