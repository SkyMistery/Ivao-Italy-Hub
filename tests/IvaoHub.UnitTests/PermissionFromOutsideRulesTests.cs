using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of a permission held from outside its department (M4, E2b, note 2026-10-01-il-permesso-non-il-dipartimento), piece
/// by piece:
/// <list type="bullet">
/// <item>the computation marks what a grant to a position gives on a department that is not the position's own — another one, or
/// every one — and what a grant to the team of a FIR gives when the division does not keep the team to its FIR; never what a grant
/// to a position gives on its own department, nor a grant to a person;</item>
/// <item>the <c>View</c> it implies is held from outside as well, and the same permission reached by a grant to the person too is
/// the person's, which lets them in;</item>
/// <item>it travels in the scope's part of the claim and reads back, and a reader that does not know it reads it closed;</item>
/// <item>it does not let its holder into the department, where a grant to a person on the same department does;</item>
/// <item>the single handler holds it on the rows of its department, as any other permission.</item>
/// </list>
/// </summary>
public sealed class PermissionFromOutsideRulesTests
{
    private const string View = "Probe.View";
    private const string Edit = "Probe.Edit";

    // The range of the events module (CONTRIBUTING.md), 761090–761099 handed to E2b.
    private const int Member = 761090;

    private static readonly PermissionCatalog Catalogue = new([
        .. CorePermissions.All,
        new PermissionDescriptor(View, IsGlobal: false),
        new PermissionDescriptor(Edit, IsGlobal: false),
    ]);

    /// <summary>A row in the care of these departments, the first its own, the way an event made with another department is one.</summary>
    private sealed class Row(params Department[] departments) : IOwnedByDepartment
    {
        public Department OwnerDepartment { get; } = departments[0];

        public int OwnerDepartmentMask { get; } = DepartmentMask.Of(departments);
    }

    [Fact]
    public void AGrantToAPositionOnADepartmentNotItsOwnGivesThePermissionFromOutside()
    {
        // The coordinators of the ATC operations, on the events: held on the events department, from outside — and the View
        // their Edit implies with it.
        var onAnother = Calculate([Atc()], [ToPosition(1, Edit, Department.AOD, scope: Department.ED)]);
        Assert.Contains(onAnother, held => held is { Name: Edit, Department: Department.ED, FromOutside: true });
        Assert.Contains(onAnother, held => held is { Name: View, Department: Department.ED, FromOutside: true });
        Assert.DoesNotContain(onAnother, held => held.Department == Department.ED && !held.FromOutside);

        // On every department: those are departments that are not the position's own too.
        var everywhere = Calculate([Atc()], [ToPosition(2, View, Department.AOD, scope: null)]);
        Assert.Contains(everywhere, held => held is { Name: View, Department: null, FromOutside: true });

        // On its own department the position is there already: nothing to mark.
        var ownDepartment = Calculate([Atc()], [ToPosition(3, View, Department.AOD, scope: Department.AOD)]);
        Assert.Contains(ownDepartment, held => held is { Name: View, Department: Department.AOD, FromOutside: false });

        // A grant to a person is the decision of 6 September, whatever their position: it lets them in.
        var byName = Calculate([Atc()], [ToPerson(4, View, Department.ED)]);
        Assert.Contains(byName, held => held is { Name: View, Department: Department.ED, FromOutside: false });

        // And the roles are what they were: an events coordinator holds the core's permissions on their own department.
        Assert.All(Calculate([Events()], []), held => Assert.False(held.FromOutside));
    }

    [Fact]
    public void TheTeamOfAFirHoldsItFromOutsideWhenTheDivisionDoesNotKeepItToItsFir()
    {
        var chief = new StaffPosition("XXAA-CH", null, StaffLevel.Coordinator, "XXAA", StaffRole.FirChief);
        UserGrant[] grants =
        [
            new()
            {
                Id = 1,
                PositionFirTeam = true,
                PositionLevels = [StaffLevel.Coordinator],
                Kind = GrantKind.Permission,
                Value = Edit,
                Department = Department.TD,
                Effect = GrantEffect.Grant,
            },
        ];

        // With all, the team holds it on the department and with no FIR: from outside, a FIR position having no department.
        var all = Calculate([chief], grants, FirStaffScope.All);
        Assert.Contains(all, held => held is { Name: Edit, Department: Department.TD, Fir: null, FromOutside: true });

        // With own, on its FIR, which says it already (M3, A11a): the claim of a FIR's chief stays as it was.
        var own = Calculate([chief], grants, FirStaffScope.Own);
        Assert.Contains(own, held => held is { Name: Edit, Department: Department.TD, Fir: "XXAA", FromOutside: false });
    }

    [Fact]
    public void ThePermissionReachedByAGrantToThePersonAsWellIsThePersons()
    {
        // The same permission on the same department, from the position's grant and from a grant by name. The ids are chosen so
        // that the order of the sources alone would keep the position's ("grant:10" before "grant:9").
        var both = Calculate(
            [Atc()],
            [ToPosition(10, View, Department.AOD, scope: Department.ED), ToPerson(9, View, Department.ED)]);

        var held = Assert.Single(both, permission => permission is { Name: View, Department: Department.ED });
        Assert.False(held.FromOutside);
        Assert.Equal($"{EffectivePermissionsCalculator.GrantSourcePrefix}9", held.Source);

        // So the person is let in, as a grant by name lets them in.
        Assert.Contains(Departments(BuildIdentity([Atc()], [.. both])), department => department == nameof(Department.ED));
    }

    [Fact]
    public void TheMarkTravelsInTheScopePartOfTheClaimAndReadsBack()
    {
        var onTheEvents = new EffectivePermission(View, Department.ED, "grant:1", FromOutside: true);
        var onOneRow = onTheEvents with { ResourceScope = "probe:row:1" };
        var everywhere = new EffectivePermission(View, null, "grant:1", FromOutside: true);

        Assert.Equal("Probe.View:ED@!", HubClaims.FormatPermission(onTheEvents));
        Assert.Equal("Probe.View:ED@!probe:row:1", HubClaims.FormatPermission(onOneRow));
        Assert.Equal("Probe.View@!", HubClaims.FormatPermission(everywhere));

        Assert.Equal(onTheEvents with { Source = "cookie" }, HubClaims.ReadPermission("Probe.View:ED@!", "cookie"));
        Assert.Equal(onOneRow with { Source = "cookie" }, HubClaims.ReadPermission("Probe.View:ED@!probe:row:1", "cookie"));
        Assert.Equal(everywhere with { Source = "cookie" }, HubClaims.ReadPermission("Probe.View@!", "cookie"));

        // Without the mark, the claim of every permission is what it always was.
        Assert.Equal("Probe.View:ED", HubClaims.FormatPermission(new EffectivePermission(View, Department.ED, "grant:1")));
        Assert.Equal(
            new EffectivePermission(View, Department.ED, "cookie"),
            HubClaims.ReadPermission("Probe.View:ED", "cookie"));
    }

    [Fact]
    public void AReaderThatDoesNotKnowTheMarkReadsItClosed()
    {
        // What every reader before E2b does — and ParsePermission still does: the mark is a piece of the scope it reads.
        var (name, department, scope) = HubClaims.ParsePermission("Probe.View:ED@!");
        Assert.Equal((View, Department.ED, "!"), (name, department, scope));

        // Held so, the permission reaches no row: none declares that scope. "Held at all" still opens a menu, and nothing else.
        EffectivePermission[] asAnOldReaderHoldsIt = [new(name, department, "cookie", scope)];
        Assert.False(PermissionSet.Has(asAnOldReaderHoldsIt, false, View, Department.ED));
        Assert.False(PermissionSet.Has(asAnOldReaderHoldsIt, false, View, Department.ED, "probe:row:1"));
        Assert.True(PermissionSet.HasAny(asAnOldReaderHoldsIt, false, View));
    }

    [Fact]
    public void APermissionFromOutsideDoesNotLetItsHolderIntoItsDepartment()
    {
        // From outside on the events department, held by a coordinator of the ATC operations: the ATC is theirs, the events not.
        var onTheEvents = BuildIdentity([Atc()], [new EffectivePermission(View, Department.ED, "grant:1", FromOutside: true)]);
        Assert.Equal([nameof(Department.AOD)], Departments(onTheEvents));

        // On every department from outside: still theirs alone, where a grant by name on every one reaches them all.
        var everywhere = BuildIdentity([Atc()], [new EffectivePermission(View, null, "grant:1", FromOutside: true)]);
        Assert.Equal([nameof(Department.AOD)], Departments(everywhere));

        // The same permission by name lets them in (note 2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento).
        var byName = BuildIdentity([Atc()], [new EffectivePermission(View, Department.ED, "grant:2")]);
        Assert.Equal([nameof(Department.AOD), nameof(Department.ED)], Departments(byName).Order(StringComparer.Ordinal));

        // And the reader of the cookie: the department of the permission is not one of theirs, and the permission is held on it.
        var user = Reading([.. onTheEvents.Claims]);
        Assert.Equal([Department.AOD], user.Departments);
        Assert.True(user.Has(View, Department.ED));
        Assert.False(user.Has(View, Department.FOD));
        Assert.True(Assert.Single(user.Permissions).FromOutside);
    }

    [Fact]
    public async Task TheHandlerHoldsItOnTheRowsOfItsDepartmentAsAnyOtherPermission()
    {
        var identity = BuildIdentity([Atc()], [new EffectivePermission(Edit, Department.ED, "grant:1", FromOutside: true)]);
        var collaborator = Reading([.. identity.Claims]);

        Assert.True(await AllowedAsync(collaborator, new Row(Department.ED), Edit));
        Assert.True(await AllowedAsync(collaborator, new Row(Department.ED, Department.SOD), Edit));
        Assert.False(await AllowedAsync(collaborator, new Row(Department.SOD), Edit));
        Assert.False(await AllowedAsync(collaborator, new Row(Department.AOD), Edit));

        // Without a row, "may they at all": yes, as for any permission held somewhere.
        Assert.True(await AllowedAsync(collaborator, resource: null, Edit));
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static StaffPosition Atc() => new("IT-AOC", Department.AOD, StaffLevel.Coordinator, null, StaffRole.AtcOps);

    private static StaffPosition Events() => new("IT-EC", Department.ED, StaffLevel.Coordinator, null, StaffRole.Events);

    private static IReadOnlyList<EffectivePermission> Calculate(
        StaffPosition[] positions,
        UserGrant[] grants,
        FirStaffScope firStaffScope = FirStaffScope.Own) =>
        EffectivePermissionsCalculator.Calculate(positions, grants, false, DateTime.UtcNow, Catalogue, firStaffScope);

    /// <summary>A grant to the coordinators of <paramref name="position"/>, held on <paramref name="scope"/>; null is every department.</summary>
    private static UserGrant ToPosition(long id, string permission, Department position, Department? scope) => new()
    {
        Id = id,
        PositionDepartment = position,
        PositionLevels = [StaffLevel.Coordinator],
        Kind = GrantKind.Permission,
        Value = permission,
        Department = scope,
        Effect = GrantEffect.Grant,
    };

    private static UserGrant ToPerson(long id, string permission, Department department) => new()
    {
        Id = id,
        Vid = Member,
        Kind = GrantKind.Permission,
        Value = permission,
        Department = department,
        Effect = GrantEffect.Grant,
    };

    private static ClaimsIdentity BuildIdentity(StaffPosition[] positions, EffectivePermission[] permissions) =>
        HubClaims.BuildIdentity(
            Member,
            firstName: "Test",
            lastName: "Outside",
            locale: "en",
            securityStamp: "stamp",
            isSuperadmin: false,
            isStaff: true,
            positions: positions,
            permissions: permissions);

    private static string[] Departments(ClaimsIdentity identity) =>
        [.. identity.FindAll(HubClaims.Department).Select(claim => claim.Value)];

    /// <summary>The host's own reader of the cookie, on an identity made of these claims.</summary>
    private static HttpContextCurrentUser Reading(Claim[] claims)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, HubClaims.CookieScheme)) };
        return new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = context }, Options.Create(Division()));
    }

    private static async Task<bool> AllowedAsync(ICurrentUser user, object? resource, string permission)
    {
        var handler = new DepartmentAuthorizationHandler(user, Options.Create(Division()), Catalogue);
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(), resource);

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static DivisionOptions Division() => new()
    {
        Code = "XX",
        CountryId = "XX",
        Domain = "hub.example.org",
        Name = new Dictionary<string, string> { ["en"] = "Example" },
        Locales = ["en"],
        DefaultLocale = "en",
        Timezone = "UTC",
    };
}
