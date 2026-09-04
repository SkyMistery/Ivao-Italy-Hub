namespace IvaoHub.Core.Division;

/// <summary>
/// Runtime overrides and small pieces of state that must survive a restart: module maintenance
/// flags, the hash of the super administrator set, the seed keys of the system templates.
/// <para>Audited, because one of those settings is a switch an administrator flips: closing a
/// module for maintenance has to leave a trace, and the trace is written by the interceptor rather
/// than by the screen that flipped it. The rows the installation writes for itself — a seeded
/// template, the hash of the super administrator set — are audited too, with VID 0, which is the
/// right answer for a thing an installation did to itself.</para>
/// </summary>
[Audited]
public sealed class DivisionSetting
{
    /// <summary>Dotted key, for example <c>modules.atc.maintenance</c>.</summary>
    public string Key { get; set; } = string.Empty;

    public string ValueJson { get; set; } = "null";

    public int UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }
}
