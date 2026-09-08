using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Content;

/// <summary>
/// One kind of thing that happens: an event, a training session, a tour, a meeting, a deadline.
/// <para>⚠️ Unlike <see cref="ContentCategory"/>, this vocabulary belongs to the <b>division</b> and
/// not to a department. Carmine decided it on 7 September 2026, after running the demo of M1 —
/// "the list is decided by headquarters, by the web team or from the admin section, and it is the
/// same for everybody" — and the note that weighed the three ways of doing it is
/// <c>decisions/2026-09-08-tipi-di-evento-di-divisione.md</c>. Two departments inventing
/// <c>Training</c> and <c>training</c> is exactly what a shared calendar must not let happen.</para>
/// <para>So it has no <c>owner_department</c>, and the CRUD engine serves it in the mode the grants
/// already use: one policy on the endpoint, no row level question, because there is no owner to
/// compare anybody against. Reading it needs <c>Calendar.View</c> — whoever may look at a calendar
/// may see what its words are — and writing it needs the global
/// <see cref="Auth.Permissions.CorePermissions.CalendarManageKinds"/>, which only the roles that
/// reach every department hold.</para>
/// <para>⚠️ There is <b>no foreign key</b> from an entry to one of these, on purpose and for the
/// same reason a content row has none to its category: an entry keeps the <see cref="Key"/> it was
/// written with, and a module projecting its own rows writes a key without asking anybody. What the
/// vocabulary decides is what a person may <i>choose</i>, which is where the two spellings came
/// from.</para>
/// </summary>
[Audited]
public sealed class CalendarKind : IAuditable
{
    public long Id { get; set; }

    /// <summary>
    /// The stable name, the one an entry stores and a filter carries in a query string. It never
    /// changes on its own: what a reader sees is <see cref="Label"/>.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public Localized<string> Label { get; set; } = Localized<string>.Empty;

    /// <summary>
    /// The colour of the chip, out of the palette the badge of the design system draws. It is a row
    /// and not a hash of the word, which is what the calendar did while this table did not exist:
    /// a colour somebody chose can group two kinds that belong together, and a hash cannot.
    /// </summary>
    public string Colour { get; set; } = string.Empty;

    /// <summary>Where the kind sits in a list of them; ties are broken by the key.</summary>
    public int Sort { get; set; }

    /// <summary>
    /// False retires a kind without deleting it: the entries written with it keep it and stay where
    /// they are, and nobody can write a new one with it.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }
}
