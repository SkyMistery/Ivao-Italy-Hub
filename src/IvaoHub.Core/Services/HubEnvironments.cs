namespace IvaoHub.Core.Services;

/// <summary>
/// The environment names this application knows about beyond the three ASP.NET Core defines.
/// </summary>
public static class HubEnvironments
{
    /// <summary>
    /// The test bench: the published package, a database of its own, and a way of signing a fake
    /// staff member in without IVAO, so that a browser can run the round the product is for
    /// (design M1 section 11.1). It is a fence, not a flag — everything it unlocks asks for this
    /// name and not for "is this development?", so that no installation drifts into it.
    /// </summary>
    public const string E2E = "E2E";
}
