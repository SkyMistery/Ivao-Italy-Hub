namespace IvaoHub.Core.Division;

/// <summary>
/// Runtime overrides and small pieces of state that must survive a restart: module maintenance
/// flags, the hash of the super administrator set, the seed keys of the system templates.
/// </summary>
public sealed class DivisionSetting
{
    /// <summary>Dotted key, for example <c>modules.atc.maintenance</c>.</summary>
    public string Key { get; set; } = string.Empty;

    public string ValueJson { get; set; } = "null";

    public int UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }
}
