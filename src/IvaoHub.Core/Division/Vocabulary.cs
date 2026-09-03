namespace IvaoHub.Core.Division;

/// <summary>
/// Owner of a row. These are the department codes IVAO itself uses, so a staff position maps onto a
/// department without a translation table (plan section 7). Stored as a string, never as a number.
/// <para>Note that the codes are not a mechanical suffix: ATC operations is <c>AOD</c> but training
/// is <c>TD</c>, and headquarters is plain <c>HQ</c>.</para>
/// </summary>
public enum Department
{
    /// <summary>
    /// Headquarters <b>of this division</b>: its director and assistant director (<c>IT-DIR</c>,
    /// <c>IT-ADIR</c>). Not to be confused with the <c>HQ-</c> prefix of a staff position, which
    /// means IVAO headquarters and belongs to no division at all: those map to
    /// <c>StaffRole.HqStaff</c> with no department (see <c>StaffRoleMap</c>).
    /// </summary>
    HQ,

    /// <summary>Special operations department.</summary>
    SOD,

    /// <summary>Flight operations department.</summary>
    FOD,

    /// <summary>ATC operations department.</summary>
    AOD,

    /// <summary>Training department.</summary>
    TD,

    /// <summary>Membership department.</summary>
    MD,

    /// <summary>Events department.</summary>
    ED,

    /// <summary>Public relations department.</summary>
    PRD,

    /// <summary>Web development department.</summary>
    WD,
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
