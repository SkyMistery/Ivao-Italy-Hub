using FluentValidation;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace IvaoHub.Core.Auth;

/// <summary>
/// What a grant has to satisfy before it is written. Two of these rules are the perimeter of the
/// whole permission model rather than tidiness, and they are stated here because this is the one
/// screen that can hand out a permission by name (plan section 6.3):
/// <list type="bullet">
/// <item>a grant only ever names a permission the catalogue knows — core or module — so that a
/// typo is a refusal and not a row that silently does nothing;</item>
/// <item>a grant may never confer a <b>global</b> permission. Who administers the hub, who reads
/// the audit log and who hands out permissions is decided by the staff positions IVAO publishes,
/// and a grant is not a way around that. The exception is a global permission the catalogue says a
/// grant may confer, a function the division hands out as it likes — who assigns the awards (M4,
/// E10f) — and then only whole, with no department;</item>
/// <item>and it may only be given to somebody this division counts as staff. The roster of the hub
/// is exactly the people who have logged in at least once, so the VID has to be one of them.</item>
/// </list>
/// Messages are i18n keys, never sentences: the browser resolves them into the language it shows.
/// </summary>
public sealed class GrantWriteDtoValidator : AbstractValidator<GrantWriteDto>
{
    /// <summary>Longest reason the column holds.</summary>
    public const int MaxReasonLength = 512;

    /// <summary>Longest permission name the column holds.</summary>
    public const int MaxValueLength = 64;

    public GrantWriteDtoValidator(PermissionCatalog catalogue, HubDbContext database, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);

        RuleFor(grant => grant.Value)
            .NotEmpty().WithMessage("errors.required")
            .MaximumLength(MaxValueLength).WithMessage("errors.text.tooLong")
            .Must(catalogue.IsKnown).WithMessage("errors.grant.unknownPermission")
            .Must(value => !catalogue.IsClosedToGrants(value)).WithMessage("errors.grant.globalPermission")
            .When(grant => !string.IsNullOrWhiteSpace(grant.Value));

        // A global permission a grant may confer is held everywhere or not at all: a department here would look like a limit
        // that nothing applies, because it is only ever asked "at all?" (M4, E10f, note 2026-10-01-chi-assegna-gli-award-con-un-grant).
        RuleFor(grant => grant.Department)
            .Null().WithMessage("errors.grant.globalDepartment")
            .When(grant => catalogue.IsKnown(grant.Value) && catalogue.IsGlobal(grant.Value) && !catalogue.IsClosedToGrants(grant.Value));

        // A member, a department's position or the team of a FIR, and exactly one of them (M2, note
        // 2026-09-13-moduli-non-subordinati-ai-dipartimenti §3.2; M3, A11a, note 2026-09-27-i-capi-fir-sul-loro-fir).
        RuleFor(grant => grant.Vid)
            .Must((grant, vid) => (vid is > 0 ? 1 : 0) + (grant.PositionDepartment is not null ? 1 : 0) + (grant.PositionFirTeam ? 1 : 0) == 1)
            .WithMessage("errors.grant.subject");

        RuleFor(grant => grant.Vid)
            .MustAsync(async (vid, cancellationToken) => await database.Users
                .AsNoTracking()
                .AnyAsync(user => user.Vid == vid && (user.IsStaff || user.IsSuperadmin), cancellationToken))
            .WithMessage("errors.grant.notStaff")
            .When(grant => grant.Vid is > 0 && grant.PositionDepartment is null && !grant.PositionFirTeam);

        // A position is a department — or the team of a FIR — at one or more levels: a department alone would be everybody in
        // it, which is a decision the staff positions already make.
        RuleFor(grant => grant.PositionLevels)
            .Must(levels => levels is { Count: > 0 })
            .WithMessage("errors.grant.levelsRequired")
            .When(grant => grant.PositionDepartment is not null || grant.PositionFirTeam);

        // The team of a FIR holds a permission on the rows of its FIR, so only a permission of an area whose rows say their FIR:
        // anywhere else it would reach no row, and say "yes" only to the question asked without one (M3, A11a, note §3.1).
        RuleFor(grant => grant.Value)
            .Must(catalogue.IsOfAnAreaWithAFir).WithMessage("errors.grant.firTeamArea")
            .When(grant => grant.PositionFirTeam && catalogue.IsKnown(grant.Value));

        // A grant that expired before it was written is a row nobody will ever notice is doing
        // nothing. It is refused now rather than debugged in six months.
        RuleFor(grant => grant.ExpiresAt)
            .Must(expiry => expiry > clock.UtcNow).WithMessage("errors.grant.alreadyExpired")
            .When(grant => grant.ExpiresAt is not null);

        RuleFor(grant => grant.Reason)
            .MaximumLength(MaxReasonLength).WithMessage("errors.text.tooLong");
    }
}
