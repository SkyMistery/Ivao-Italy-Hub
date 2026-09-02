namespace IvaoHub.Core.Division;

/// <summary>
/// Owner of a row. Same vocabulary as the staff positions, so a position maps onto a department
/// without a translation table (plan section 7). Stored as a string, never as a number.
/// </summary>
public enum Department
{
    /// <summary>Division headquarters: director and assistant director.</summary>
    HQ,

    /// <summary>Special operations.</summary>
    SO,

    /// <summary>Flight operations.</summary>
    FO,

    /// <summary>ATC operations.</summary>
    AO,

    /// <summary>Training.</summary>
    TR,

    /// <summary>Membership.</summary>
    MB,

    /// <summary>Events.</summary>
    EV,

    /// <summary>Public relations.</summary>
    PR,

    /// <summary>Web development.</summary>
    WM,
}

/// <summary>Who may read a row. The global query filter of F4 turns this into a where clause.</summary>
public enum Visibility
{
    /// <summary>Everyone, including anonymous visitors.</summary>
    Public,

    /// <summary>Any authenticated member.</summary>
    Members,

    /// <summary>Any staff member of the division.</summary>
    Staff,

    /// <summary>Only the department that owns the row.</summary>
    Department,
}

/// <summary>Editorial state. The public site only ever reads published rows.</summary>
public enum PublishStatus
{
    Draft,
    Published,
}

/// <summary>
/// Seniority of a staff position inside its department. The vocabulary lives here because the
/// column needs it; <c>StaffRoleMap</c>, which produces it from a raw IVAO position, arrives in F2.
/// </summary>
public enum StaffLevel
{
    Coordinator,
    Assistant,
    Advisor,

    /// <summary>Member of the department, for example a trainer (T01-T99).</summary>
    Member,
}

/// <summary>How far the authority of a FIR team reaches; chosen by the division, not by the code.</summary>
public enum FirStaffScope
{
    /// <summary>FIR teams work on the content of every FIR (default, and what vIPI does today).</summary>
    All,

    /// <summary>Each FIR team only reaches the content of its own FIR.</summary>
    Own,
}
