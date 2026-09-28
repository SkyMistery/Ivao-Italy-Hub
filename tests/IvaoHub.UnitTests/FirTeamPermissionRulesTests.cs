using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rules of a permission held on one FIR (M3, A11a, note 2026-09-27-i-capi-fir-sul-loro-fir), piece by piece:
/// <list type="bullet">
/// <item>it reaches the rows of that FIR and no other, and counts for "held at all";</item>
/// <item>it travels in the scope's part of the claim, so a reader that does not know it reads it closed, and a department the
/// reader cannot read is never "every department" (the reviewer's point 1);</item>
/// <item>the computation writes it with <c>own</c> and not with <c>all</c>, one per FIR the member heads, and never for a grant to
/// a person or to a department's position;</item>
/// <item>it does not let its holder into the department;</item>
/// <item>the handler holds the team to its FIR, and never the staff of a department — whatever <c>firStaffScope</c> says;</item>
/// <item>a grant to the team names a permission of an area whose rows say their FIR, or the screen and the seed refuse it (the
/// reviewer's point 3), and the division file names either a department or the team.</item>
/// </list>
/// </summary>
public sealed class FirTeamPermissionRulesTests
{
    private const string View = "Probe.View";
    private const string Edit = "Probe.Edit";
    private const string Assign = "Probe.Assign";

    // The range of the training module (CONTRIBUTING.md), handed to A11a: 790080–790089.
    private const int Member = 790089;

    private const string Fir = "XXAA";
    private const string OtherFir = "XXBB";
    private const string ThirdFir = "XXCC";

    private static readonly PermissionCatalog Catalogue = new([
        .. CorePermissions.All,
        new PermissionDescriptor(View, IsGlobal: false),
        new PermissionDescriptor(Edit, IsGlobal: false),
        new PermissionDescriptor(Assign, IsGlobal: false),
    ]);

    /// <summary>A row of a FIR, the way a training of a position is one; or of none, the way a pilot's training is.</summary>
    private sealed class Row(string? fir) : IOwnedByDepartment, IHasFir
    {
        public Department OwnerDepartment { get; set; } = Department.TD;

        public string? Fir { get; } = fir;
    }

    /// <summary>A row of the same department that does not say a FIR at all, the way a ban is one.</summary>
    private sealed class RowWithNoFir : IOwnedByDepartment
    {
        public Department OwnerDepartment { get; set; } = Department.TD;
    }

    [Fact]
    public void APermissionHeldOnAFirReachesTheRowsOfThatFirAndNoOther()
    {
        EffectivePermission[] onOurFir = [new(Assign, Department.TD, "grant:1", Fir: Fir)];

        Assert.True(PermissionSet.Has(onOurFir, false, Assign, Department.TD, null, Fir));
        Assert.True(PermissionSet.Has(onOurFir, false, Assign, Department.TD, null, Fir.ToLowerInvariant()));
        Assert.False(PermissionSet.Has(onOurFir, false, Assign, Department.TD, null, OtherFir));
        Assert.False(PermissionSet.Has(onOurFir, false, Assign, Department.TD, null, fir: null));
        Assert.False(PermissionSet.Has(onOurFir, false, Assign, Department.TD));
        Assert.False(PermissionSet.Has(onOurFir, false, Assign, Department.FOD, null, Fir));

        // "Held at all" it is: that opens the menu and the list.
        Assert.True(PermissionSet.HasAny(onOurFir, false, Assign));

        // Held with no FIR, it reaches every row whatever its FIR, as it always did.
        EffectivePermission[] onTheDepartment = [new(Assign, Department.TD, "role:TD/Coordinator")];
        Assert.True(PermissionSet.Has(onTheDepartment, false, Assign, Department.TD, null, Fir));
        Assert.True(PermissionSet.Has(onTheDepartment, false, Assign, Department.TD, null, fir: null));
    }

    [Fact]
    public void TheFirTravelsInTheScopePartOfTheClaimAndReadsBack()
    {
        var onOurFir = new EffectivePermission(Assign, Department.TD, "grant:1", Fir: Fir);
        var both = new EffectivePermission(Assign, Department.TD, "grant:1", "probe:row:1", Fir);

        Assert.Equal("Probe.Assign:TD@#XXAA", HubClaims.FormatPermission(onOurFir));
        Assert.Equal("Probe.Assign:TD@probe:row:1#XXAA", HubClaims.FormatPermission(both));
        Assert.Equal("Probe.Edit:TD", HubClaims.FormatPermission(new EffectivePermission(Edit, Department.TD, "grant:1")));

        Assert.Equal(onOurFir with { Source = "cookie" }, HubClaims.ReadPermission("Probe.Assign:TD@#XXAA", "cookie"));
        Assert.Equal(both with { Source = "cookie" }, HubClaims.ReadPermission("Probe.Assign:TD@probe:row:1#XXAA", "cookie"));
        Assert.Equal(
            new EffectivePermission(Edit, Department.TD, "cookie", "probe:row:1"),
            HubClaims.ReadPermission("Probe.Edit:TD@probe:row:1", "cookie"));
        Assert.Equal(new EffectivePermission(Edit, null, "cookie"), HubClaims.ReadPermission("Probe.Edit", "cookie"));
    }

    [Fact]
    public void AReaderThatDoesNotKnowTheFirReadsItClosed()
    {
        // What every reader before A11a does — and ParsePermission still does: the FIR is part of the scope it reads.
        var (name, department, scope) = HubClaims.ParsePermission("Probe.Assign:TD@#XXAA");
        Assert.Equal((Assign, Department.TD, "#XXAA"), (name, department, scope));

        // Held so, the permission reaches no row: none declares that scope, with or without a scope of its own.
        EffectivePermission[] asAnOldReaderHoldsIt = [new(name, department, "cookie", scope)];
        Assert.False(PermissionSet.Has(asAnOldReaderHoldsIt, false, Assign, Department.TD));
        Assert.False(PermissionSet.Has(asAnOldReaderHoldsIt, false, Assign, Department.TD, "probe:row:1"));
        Assert.True(PermissionSet.HasAny(asAnOldReaderHoldsIt, false, Assign));
    }

    [Fact]
    public void AnUnreadableDepartmentIsNotEveryDepartment()
    {
        // A department this hub does not know: the claim is worth nothing, where a missing department is worth every one.
        Assert.Null(HubClaims.ReadPermission("Probe.Edit:NOPE", "cookie"));
        Assert.Throws<FormatException>(() => HubClaims.ParsePermission("Probe.Edit:NOPE"));

        // A FIR with no name is no FIR either.
        Assert.Null(HubClaims.ReadPermission("Probe.Assign:TD@#", "cookie"));

        // And the reader of the cookie holds nothing of it.
        var user = Reading(
            new Claim(HubClaims.Permission, "Probe.Edit:NOPE"),
            new Claim(HubClaims.Permission, "Probe.Assign:TD@#"));

        Assert.False(user.Has(Edit, Department.TD));
        Assert.False(user.Has(Edit, Department.FOD));
        Assert.False(user.HasAny(Edit));
        Assert.False(user.HasAny(Assign));
        Assert.Empty(user.Permissions);
    }

    [Fact]
    public void TheComputationHoldsATeamGrantOnTheFirOfEachPositionWithOwnAndOnTheDepartmentWithAll()
    {
        // One member heads two FIRs, and advises a third: the grant is for chiefs and assistant chiefs.
        StaffPosition[] positions =
        [
            FirPosition(Fir, StaffLevel.Coordinator, StaffRole.FirChief),
            FirPosition(OtherFir, StaffLevel.Assistant, StaffRole.FirAssistantChief),
            FirPosition(ThirdFir, StaffLevel.Advisor, StaffRole.FirAdvisor),
        ];
        UserGrant[] grants = [TeamGrant(1, Assign, StaffLevel.Coordinator, StaffLevel.Assistant), TeamGrant(2, Edit, StaffLevel.Coordinator)];

        var own = EffectivePermissionsCalculator.Calculate(positions, grants, false, DateTime.UtcNow, Catalogue, FirStaffScope.Own);

        Assert.Equal([Fir, OtherFir], own.Where(held => held.Name == Assign).Select(held => held.Fir));
        Assert.All(own, held => Assert.Equal(Department.TD, held.Department));

        // A permission of an area implies the area's View — Edit, and in this calculator every other one — on the same FIR and
        // on no other: here Assign on both FIRs, and Edit on the first.
        Assert.Equal([Fir, OtherFir], own.Where(held => held.Name == View).Select(held => held.Fir));

        var all = EffectivePermissionsCalculator.Calculate(positions, grants, false, DateTime.UtcNow, Catalogue, FirStaffScope.All);
        Assert.Equal([(string?)null], all.Where(held => held.Name == Assign).Select(held => held.Fir));

        // Without saying, the computation is the one of a division that does not keep FIR teams to their FIR.
        Assert.Equal(all, EffectivePermissionsCalculator.Calculate(positions, grants, false, DateTime.UtcNow, Catalogue));
    }

    [Fact]
    public void ATeamGrantIsNeitherADepartmentsNorAPersonsAndTheirsAreNotTheTeams()
    {
        var chief = FirPosition(Fir, StaffLevel.Coordinator, StaffRole.FirChief);
        var coordinator = new StaffPosition("IT-TC", Department.TD, StaffLevel.Coordinator, null, StaffRole.Training);

        // The coordinator of a department does not hold a grant to the team of a FIR at their level...
        Assert.DoesNotContain(
            EffectivePermissionsCalculator.Calculate([coordinator], [TeamGrant(1, Assign, StaffLevel.Coordinator)], false, DateTime.UtcNow, Catalogue, FirStaffScope.Own),
            held => held.Name == Assign);

        // ...nor does the chief of a FIR hold a grant to a department's position at theirs.
        var toTheDepartment = new UserGrant
        {
            Id = 2,
            PositionDepartment = Department.TD,
            PositionLevels = [StaffLevel.Coordinator],
            Kind = GrantKind.Permission,
            Value = Assign,
            Department = Department.TD,
            Effect = GrantEffect.Grant,
        };
        Assert.DoesNotContain(
            EffectivePermissionsCalculator.Calculate([chief], [toTheDepartment], false, DateTime.UtcNow, Catalogue, FirStaffScope.Own),
            held => held.Name == Assign);

        // And a grant to the chief by name is theirs across the department, with no FIR: a person's, not the team's.
        var byName = new UserGrant
        {
            Id = 3,
            Vid = Member,
            Kind = GrantKind.Permission,
            Value = Assign,
            Department = Department.TD,
            Effect = GrantEffect.Grant,
        };
        var theirs = Assert.Single(
            EffectivePermissionsCalculator.Calculate([chief], [byName], false, DateTime.UtcNow, Catalogue, FirStaffScope.Own),
            permission => permission.Name == Assign);
        Assert.Null(theirs.Fir);
    }

    [Fact]
    public void APermissionHeldOnAFirDoesNotLetItsHolderIntoItsDepartment()
    {
        var onOurFir = BuildIdentity([new EffectivePermission(Assign, Department.TD, "grant:1", Fir: Fir)]);
        Assert.DoesNotContain(onOurFir.FindAll(HubClaims.Department), claim => claim.Value == nameof(Department.TD));

        var onTheDepartment = BuildIdentity([new EffectivePermission(Assign, Department.TD, "grant:1")]);
        Assert.Contains(onTheDepartment.FindAll(HubClaims.Department), claim => claim.Value == nameof(Department.TD));
    }

    [Fact]
    public async Task TheHandlerHoldsTheTeamToItsFirAndNeverTheStaffOfADepartment()
    {
        // The chief of a FIR, with a permission of the team on their FIR.
        var chief = Signed(new EffectivePermission(Assign, Department.TD, "grant:1", Fir: Fir));

        Assert.True(await AllowedAsync(chief, new Row(Fir), Assign, FirStaffScope.Own));
        Assert.False(await AllowedAsync(chief, new Row(OtherFir), Assign, FirStaffScope.Own));
        Assert.False(await AllowedAsync(chief, new Row(fir: null), Assign, FirStaffScope.Own));
        Assert.False(await AllowedAsync(chief, new RowWithNoFir(), Assign, FirStaffScope.Own));

        // Without a row the question is "may they at all", as for a permission granted on one row.
        Assert.True(await AllowedAsync(chief, resource: null, Assign, FirStaffScope.Own));

        // The coordinator of the department holds it with no FIR and no FIR of their own: every row, even with own — which the
        // rule that stood here until A11a refused them on every row of a FIR.
        var coordinator = Signed(new EffectivePermission(Assign, Department.TD, "role:TD/Coordinator"));
        foreach (var scope in new[] { FirStaffScope.Own, FirStaffScope.All })
        {
            Assert.True(await AllowedAsync(coordinator, new Row(Fir), Assign, scope));
            Assert.True(await AllowedAsync(coordinator, new Row(OtherFir), Assign, scope));
            Assert.True(await AllowedAsync(coordinator, new Row(fir: null), Assign, scope));
        }
    }

    [Fact]
    public void AGrantToTheTeamNamesAPermissionOfAnAreaWhoseRowsSayTheirFir()
    {
        var catalogue = new PermissionCatalog([.. CorePermissions.All, new PermissionDescriptor(Assign, IsGlobal: false)]);

        // Until the hub has read its models, no area has rows with a FIR.
        Assert.False(catalogue.IsOfAnAreaWithAFir(Assign));

        catalogue.LearnAreasWithAFir(["Probe"]);
        Assert.True(catalogue.IsOfAnAreaWithAFir(Assign));
        Assert.False(catalogue.IsOfAnAreaWithAFir(CorePermissions.LinksView));
        Assert.False(catalogue.IsOfAnAreaWithAFir("Probe"));
    }

    [Fact]
    public async Task TheScreenRefusesATeamGrantOnAnAreaWithNoFirAndASecondSubject()
    {
        var catalogue = new PermissionCatalog([.. CorePermissions.All, new PermissionDescriptor(Assign, IsGlobal: false)]);
        catalogue.LearnAreasWithAFir(["Probe"]);

        // A context that is never opened: the rule that reads the table asks about a VID, and none is given here.
        await using var database = new HubDbContext(new DbContextOptionsBuilder<HubDbContext>()
            .UseMySql("Server=localhost;Database=none;User ID=none;Password=none", new MariaDbServerVersion(HubDbContext.ServerVersion))
            .Options);
        var validator = new GrantWriteDtoValidator(catalogue, database, new SystemClock());
        var token = TestContext.Current.CancellationToken;

        Assert.True((await validator.ValidateAsync(TeamWrite(Assign, [StaffLevel.Coordinator]), token)).IsValid);

        var elsewhere = await validator.ValidateAsync(TeamWrite(CorePermissions.LinksView, [StaffLevel.Coordinator]), token);
        Assert.Contains(elsewhere.Errors, error => error.PropertyName == "Value" && error.ErrorMessage == "errors.grant.firTeamArea");

        var twoSubjects = await validator.ValidateAsync(
            TeamWrite(Assign, [StaffLevel.Coordinator]) with { PositionDepartment = Department.TD },
            token);
        Assert.Contains(twoSubjects.Errors, error => error.ErrorMessage == "errors.grant.subject");

        var noLevel = await validator.ValidateAsync(TeamWrite(Assign, []), token);
        Assert.Contains(noLevel.Errors, error => error.ErrorMessage == "errors.grant.levelsRequired");
    }

    [Fact]
    public void TheDivisionFileNamesADepartmentOrTheTeamOfAFir()
    {
        var validator = new DivisionOptionsValidator();

        Assert.DoesNotContain(Failures(validator, new PositionGrantSeed { FirTeam = true, Levels = [StaffLevel.Coordinator], Permission = Assign }), IsAboutTheFirstSeed);
        Assert.DoesNotContain(Failures(validator, new PositionGrantSeed { Department = Department.TD, Levels = [StaffLevel.Coordinator], Permission = Assign }), IsAboutTheFirstSeed);
        Assert.Contains(Failures(validator, new PositionGrantSeed { Department = Department.TD, FirTeam = true, Levels = [StaffLevel.Coordinator], Permission = Assign }), IsAboutTheFirstSeed);
        Assert.Contains(Failures(validator, new PositionGrantSeed { Levels = [StaffLevel.Coordinator], Permission = Assign }), IsAboutTheFirstSeed);

        // And the seed remembers the team apart from any department; a department's seed is remembered as it always was.
        var team = PositionGrantSeeder.Fingerprint(new PositionGrantSeed { FirTeam = true, Levels = [StaffLevel.Coordinator], Permission = Assign });
        var department = PositionGrantSeeder.Fingerprint(new PositionGrantSeed { Department = Department.TD, Levels = [StaffLevel.Coordinator], Permission = Assign });
        Assert.Equal("firTeam|Coordinator|Probe.Assign|*|grant", team);
        Assert.Equal("TD|Coordinator|Probe.Assign|*|grant", department);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static StaffPosition FirPosition(string fir, StaffLevel level, StaffRole role) =>
        new($"{fir}-{role}", null, level, fir, role);

    private static UserGrant TeamGrant(long id, string permission, params StaffLevel[] levels) => new()
    {
        Id = id,
        PositionFirTeam = true,
        PositionLevels = levels,
        Kind = GrantKind.Permission,
        Value = permission,
        Department = Department.TD,
        Effect = GrantEffect.Grant,
    };

    private static GrantWriteDto TeamWrite(string permission, StaffLevel[] levels) => new(
        Vid: null,
        Kind: GrantKind.Permission,
        Value: permission,
        Department: Department.TD,
        Effect: GrantEffect.Grant,
        ExpiresAt: null,
        Reason: null,
        RowVersion: default,
        PositionLevels: levels,
        PositionFirTeam: true);

    private static ClaimsIdentity BuildIdentity(EffectivePermission[] permissions) =>
        HubClaims.BuildIdentity(
            Member,
            firstName: "Test",
            lastName: "FirTeam",
            locale: "en",
            securityStamp: "stamp",
            isSuperadmin: false,
            isStaff: true,
            positions: [],
            permissions: permissions);

    private static HttpContextCurrentUser Signed(params EffectivePermission[] permissions) =>
        Reading([.. BuildIdentity(permissions).Claims]);

    /// <summary>The host's own reader of the cookie, on an identity made of these claims.</summary>
    private static HttpContextCurrentUser Reading(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims.Prepend(new Claim(HubClaims.Vid, Member.ToString(System.Globalization.CultureInfo.InvariantCulture))), HubClaims.CookieScheme);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = context }, Options.Create(Division(FirStaffScope.Own)));
    }

    private static async Task<bool> AllowedAsync(ICurrentUser user, object? resource, string permission, FirStaffScope scope)
    {
        var handler = new DepartmentAuthorizationHandler(user, Options.Create(Division(scope)), Catalogue);
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(), resource);

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static DivisionOptions Division(FirStaffScope scope) => new()
    {
        Code = "XX",
        CountryId = "XX",
        Domain = "hub.example.org",
        Name = new Dictionary<string, string> { ["en"] = "Example" },
        Locales = ["en"],
        DefaultLocale = "en",
        Timezone = "UTC",
        FirStaffScope = scope,
    };

    private static IEnumerable<string> Failures(DivisionOptionsValidator validator, PositionGrantSeed seed) =>
        validator.Validate(name: null, Division(FirStaffScope.Own) with { PositionGrants = [seed] }).Failures ?? [];

    private static bool IsAboutTheFirstSeed(string failure) =>
        failure.Contains("'positionGrants[0]'", StringComparison.Ordinal) && failure.Contains("firTeam", StringComparison.Ordinal);
}
