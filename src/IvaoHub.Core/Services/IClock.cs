namespace IvaoHub.Core.Services;

/// <summary>
/// The current time, injected so that a test can decide what "now" is. Everything the hub stores
/// is UTC; the time zone of the division is only ever used to show a local time next to it.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>The real clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
