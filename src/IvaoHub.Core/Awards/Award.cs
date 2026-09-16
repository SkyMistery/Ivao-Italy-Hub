using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;

namespace IvaoHub.Core.Awards;

/// <summary>
/// One award of the division's catalogue (plan section 9.1): what it is called, what it looks like
/// and what somebody has to have done to deserve it. Awards themselves are IVAO's — the member sees
/// the ones assigned to them on their IVAO profile — and the hub keeps the register of who assigned
/// which, and why (M2, T4b).
/// <para>It belongs to a department, like a link: the flight operations department writes the award
/// of its tours, and edits it with <c>Awards.Edit</c> on its own department (decided with Carmine,
/// 16 September 2026). Assigning one is a different act behind the global <c>Awards.Assign</c>, and
/// whoever assigns has to read every award of every department — so the catalogue is shared for
/// reading as a whole.</para>
/// <para>Retired with <see cref="IsActive"/> rather than deleted once somebody has received it: an
/// assignment names the award it was, and the register has to keep reading.</para>
/// </summary>
[Audited]
public sealed class Award : IOwnedByDepartment, IAuditable, IProjectable, ISharedForReading
{
    /// <summary>The catalogue is read by every department; writing it stays with the owner.</summary>
    bool ISharedForReading.IsSharedForReading => true;

    public long Id { get; set; }

    public Department OwnerDepartment { get; set; }

    public Localized<string> Name { get; set; } = Localized<string>.Empty;

    /// <summary>What the award is, as the staff reads it before assigning it.</summary>
    public Localized<string>? Description { get; set; }

    /// <summary>What a member has to have done: the sentence whoever assigns it checks against.</summary>
    public Localized<string>? Criteria { get; set; }

    /// <summary>
    /// The picture, from the media library. IVAO's documentation of award images answers 403, so the
    /// image is uploaded by hand (design M2 section 11, extension 7).
    /// </summary>
    public long? ImageMediaId { get; set; }

    /// <summary>False retires the award: nobody can assign it again, and what was assigned stays.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime RowVersion { get; set; }

    string IProjectable.SourceModule => ProjectionSource.Core;

    string IProjectable.SourceId => $"award:{Id}";

    /// <summary>
    /// An award is nothing a reader searches for and has no date: all it projects is its picture, as a
    /// use without an end, so the library never deletes a file an award still shows (T4b).
    /// </summary>
    ProjectionSnapshot? IProjectable.Project(ProjectionContext context) =>
        ImageMediaId is { } image ? new ProjectionSnapshot(null, [], [], [new MediaUseProjection(image, null)]) : null;
}
