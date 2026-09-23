using IvaoHub.Core.Auth.Permissions;

namespace IvaoHub.Modules.FlightOps;

/// <summary>
/// The permissions of the tours (design M2 §7.1). All of them are held on a department — the base
/// department of the module — and who holds them is <c>division.json → positionGrants</c> (§7.2), never
/// this file.
/// </summary>
public static class TourPermissions
{
    /// <summary>The area the CRUD engine derives <c>Tours.View</c> and <c>Tours.Edit</c> from.</summary>
    public const string Area = "Tours";

    public const string View = "Tours.View";
    public const string Edit = "Tours.Edit";
    public const string Delete = "Tours.Delete";
    public const string ManageRules = "Tours.ManageRules";
    public const string ManageTemplates = "Tours.ManageTemplates";
    public const string ManageAircraft = "Tours.ManageAircraft";

    /// <summary>Taking and deciding a PIREP; grantable on one tour, and never on a PIREP of one's own (§7.3).</summary>
    public const string Validate = "Tours.Validate";

    /// <summary>
    /// Reopening a decision somebody else took (§4.2.1): coordinator and assistant. Whoever took it reopens their own with
    /// <see cref="Validate"/> alone. Never on a report of one's own (note 2026-09-23-la-validazione §2.4).
    /// </summary>
    public const string ReopenDecisions = "Tours.ReopenDecisions";

    public const string ManageValidators = "Tours.ManageValidators";
    public const string ViewPilots = "Tours.ViewPilots";
    public const string Ban = "Tours.Ban";
    public const string ManageSettings = "Tours.ManageSettings";

    public static readonly IReadOnlyList<PermissionDescriptor> All =
    [
        new(View, IsGlobal: false),
        new(Edit, IsGlobal: false),
        new(Delete, IsGlobal: false),
        new(ManageRules, IsGlobal: false),
        new(ManageTemplates, IsGlobal: false),
        new(ManageAircraft, IsGlobal: false),
        new(Validate, IsGlobal: false, DeniedToStakeholder: true),
        new(ReopenDecisions, IsGlobal: false, DeniedToStakeholder: true),
        new(ManageValidators, IsGlobal: false),
        new(ViewPilots, IsGlobal: false),
        new(Ban, IsGlobal: false),
        new(ManageSettings, IsGlobal: false),
    ];
}
