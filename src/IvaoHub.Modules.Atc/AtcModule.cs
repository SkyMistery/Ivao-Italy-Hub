namespace IvaoHub.Modules.Atc;

/// <summary>
/// Placeholder for the ATC module. It becomes a real <c>IModule</c> in F8, once the module
/// contract and the registry exist in the core (design M0 sections 6.1 and 6.4).
/// </summary>
public sealed class AtcModule
{
    /// <summary>Module key, used in <c>division.modules</c> and as the <c>/api/{key}</c> prefix.</summary>
    public string Key => "atc";
}
