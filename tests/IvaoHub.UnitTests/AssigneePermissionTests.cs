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
/// A permission that reaches only the rows assigned to whoever asks (M3, A3b, note 2026-09-26-le-righe-affidate-a-chi-scrive),
/// asked of the single handler through the very cookie a login writes, and what the catalogue refuses so that the rule never
/// fails in silence:
/// <list type="bullet">
/// <item>on a row assigned to the asker the marked permission counts; on any other row — assigned to somebody else, to nobody,
/// or of an entity that says nothing about it — it is worth what the area's <c>Edit</c> is worth there;</item>
/// <item>without a row the question is still "held at all?", which is what offers an examiner "new exam";</item>
/// <item>the catalogue never marks a permission that reads, and refuses <c>AlsoOnDeletion</c> on a permission that is not marked
/// and a marked permission on an entity that has no assignee (the reviewer's points 1 and 2).</item>
/// </list>
/// </summary>
public sealed class AssigneePermissionTests
{
    private const string View = "Probe.View";
    private const string Edit = "Probe.Edit";
    private const string Manage = "Probe.Manage";
    private const string Decide = "Probe.Decide";
    private const int Examiner = 790050;
    private const int Other = 790051;

    private static readonly PermissionCatalog Catalogue = new([
        .. CorePermissions.All,
        new PermissionDescriptor(View, IsGlobal: false),
        new PermissionDescriptor(Edit, IsGlobal: false),
        new PermissionDescriptor(Manage, IsGlobal: false, OnlyForAssignee: true),
        new PermissionDescriptor(Decide, IsGlobal: false),
    ]);

    private static readonly DivisionOptions Division = new()
    {
        Code = "XX",
        Locales = ["en"],
        DefaultLocale = "en",
    };

    /// <summary>An exam: in the care of the training department, assigned to its examiner.</summary>
    private sealed class Exam(int? examiner) : IOwnedByDepartment, IHasAssignee
    {
        public Department OwnerDepartment => Department.TD;

        public int? AssigneeVid { get; } = examiner;
    }

    /// <summary>A row of the same department that says nothing about whom it is assigned to.</summary>
    private sealed class Note : IOwnedByDepartment
    {
        public Department OwnerDepartment => Department.TD;
    }

    [AlsoWrittenWith(Manage, AlsoOnCreation = true, AlsoOnDeletion = true)]
    [AlsoWrittenWith(Decide, AlsoOnCreation = true)]
    private sealed class WrittenAsTheRuleSays : IOwnedByDepartment, IHasAssignee
    {
        public Department OwnerDepartment => Department.TD;

        public int? AssigneeVid => null;
    }

    [AlsoWrittenWith(Decide, AlsoOnDeletion = true)]
    private sealed class DeletedWithAnUnmarkedPermission : IOwnedByDepartment, IHasAssignee
    {
        public Department OwnerDepartment => Department.TD;

        public int? AssigneeVid => null;
    }

    [AlsoWrittenWith(Manage)]
    private sealed class MarkedWithNoAssignee : IOwnedByDepartment
    {
        public Department OwnerDepartment => Department.TD;
    }

    [Fact]
    public async Task AMarkedPermissionReachesARowAssignedToTheAskerAndNoOther()
    {
        var examiner = SignedIn(Examiner, superadmin: false, Held(Manage));

        Assert.True(await AllowedAsync(examiner, new Exam(Examiner), Manage));
        Assert.False(await AllowedAsync(examiner, new Exam(Other), Manage));
        Assert.False(await AllowedAsync(examiner, new Exam(examiner: null), Manage));
        Assert.False(await AllowedAsync(examiner, new Note(), Manage));
    }

    [Fact]
    public async Task OnAnyOtherRowItIsWorthWhatTheAreasEditIsWorth()
    {
        // A coordinator holds the marked permission and Edit on the department.
        var coordinator = SignedIn(Other, superadmin: false, Held(Manage), Held(Edit));
        Assert.True(await AllowedAsync(coordinator, new Exam(Examiner), Manage));
        Assert.True(await AllowedAsync(coordinator, new Exam(examiner: null), Manage));
        Assert.True(await AllowedAsync(coordinator, new Note(), Manage));

        // The roles that reach every department hold both with no department at all.
        var director = SignedIn(Other, superadmin: false, Held(Manage, everywhere: true), Held(Edit, everywhere: true));
        Assert.True(await AllowedAsync(director, new Exam(Examiner), Manage));

        // A super administrator holds everything.
        Assert.True(await AllowedAsync(SignedIn(Other, superadmin: true), new Exam(Examiner), Manage));

        // Edit on another department is not Edit on this row.
        var elsewhere = SignedIn(Other, superadmin: false, Held(Manage), new EffectivePermission(Edit, Department.ED, "role:test"));
        Assert.False(await AllowedAsync(elsewhere, new Exam(Examiner), Manage));
    }

    [Fact]
    public async Task WithoutARowTheQuestionIsStillWhetherThePermissionIsHeldAtAll()
    {
        Assert.True(await AllowedAsync(SignedIn(Examiner, superadmin: false, Held(Manage)), resource: null, Manage));
        Assert.False(await AllowedAsync(SignedIn(Examiner, superadmin: false, Held(View)), resource: null, Manage));
    }

    [Fact]
    public async Task APermissionThatIsNotMarkedDoesNotLookAtTheAssignee()
    {
        var other = SignedIn(Other, superadmin: false, Held(Decide), Held(View));

        Assert.True(await AllowedAsync(other, new Exam(Examiner), Decide));
        Assert.True(await AllowedAsync(other, new Exam(Examiner), View));
    }

    [Fact]
    public void TheCatalogueSaysWhichPermissionIsMarkedAndWhichEditItFallsBackOn()
    {
        Assert.True(Catalogue.IsOnlyForAssignee(Manage));
        Assert.False(Catalogue.IsOnlyForAssignee(Decide));
        Assert.False(Catalogue.IsOnlyForAssignee("Probe.Unknown"));

        Assert.Equal(Edit, Catalogue.EditOf(Manage));
        Assert.Equal(View, Catalogue.ViewOf(Manage));
        Assert.Null(Catalogue.EditOf(CorePermissions.AuditView));
    }

    [Fact]
    public void TheCatalogueNeverMarksAPermissionThatReads()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => new PermissionCatalog([
            new PermissionDescriptor(View, IsGlobal: false, OnlyForAssignee: true),
            new PermissionDescriptor(Edit, IsGlobal: false),
        ]));

        Assert.Contains(View, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeletingWithAnAlternativeWhosePermissionIsNotMarkedIsRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => Catalogue.VerifyAlternatives([typeof(DeletedWithAnUnmarkedPermission)]));

        Assert.Contains(nameof(DeletedWithAnUnmarkedPermission), refused.Message, StringComparison.Ordinal);
        Assert.Contains("AlsoOnDeletion", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMarkedPermissionOnAnEntityWithNoAssigneeIsRefused()
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => Catalogue.VerifyAlternatives([typeof(MarkedWithNoAssignee)]));

        Assert.Contains(nameof(MarkedWithNoAssignee), refused.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IHasAssignee), refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatTheRuleCanHonourPasses() =>
        Assert.Null(Record.Exception(() => Catalogue.VerifyAlternatives(
            [typeof(WrittenAsTheRuleSays), typeof(Exam), typeof(Note)])));

    // ---- helpers ---------------------------------------------------------------------------------

    /// <summary>A permission held on the training department, or on every department as the roles that reach them all.</summary>
    private static EffectivePermission Held(string permission, bool everywhere = false) =>
        new(permission, everywhere ? null : Department.TD, "role:test");

    private static async Task<bool> AllowedAsync(ICurrentUser user, object? resource, string permission)
    {
        var handler = new DepartmentAuthorizationHandler(user, Options.Create(Division), Catalogue);
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(), resource);

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static HttpContextCurrentUser SignedIn(int vid, bool superadmin, params EffectivePermission[] permissions)
    {
        var identity = HubClaims.BuildIdentity(
            vid: vid,
            firstName: "Test",
            lastName: "Examiner",
            locale: "en",
            securityStamp: "stamp",
            isSuperadmin: superadmin,
            isStaff: true,
            positions: [],
            permissions: permissions);

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = context }, Options.Create(Division));
    }
}
