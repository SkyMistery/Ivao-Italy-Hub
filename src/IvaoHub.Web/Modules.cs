using IvaoHub.Core.Modules;
using IvaoHub.Modules.FlightOps;

namespace IvaoHub.Web;

/// <summary>
/// Every module of this build, named one by one. This file and
/// <c>web/src/modules/index.ts</c> are the two places a module is added, and adding one is a line
/// in each (design M0 section 6.5).
/// <para>Deliberately a list and not a scan of the assemblies <c>IvaoHub.Web</c> references. A scan
/// reads identically to a list on the day it is written and differently on the day a transitive
/// reference brings in something that happens to implement <see cref="IModule"/>; and it makes
/// "which modules does this build have?" a question you answer by running the application rather
/// than by opening a file.</para>
/// <para>Empty from 13 September 2026, when the ATC module left together with vIPI (note
/// 2026-09-13-staccarsi-da-vipi), until the tours opened M2 (T5, 16 September 2026).</para>
/// <para>The order is the order menu entries come out in.</para>
/// </summary>
internal static class Modules
{
    public static readonly IReadOnlyList<IModule> All = [new FlightOpsModule()];
}
