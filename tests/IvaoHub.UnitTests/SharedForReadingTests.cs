using System.Security.Claims;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Division;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// The rows every department may read, and the two ways the question gets asked.
///
/// <para>⚠️ These live here rather than only over the wire because the end to end suite cannot tell
/// them apart. A coordinator writing a template of another department is refused <b>twice</b> — by
/// this handler and again by the save changes interceptor — so an end to end test stays green even
/// with the rule below deleted, and that is a test that proves nothing (implementation plan M1
/// §A.10, verified by breaking it). This is where the handler answers on its own.</para>
///
/// <para>The other half is the pairing: the CRUD engine narrows a list in SQL from
/// <c>ContentEntry.SharedForReading</c>, and the handler asks the row in memory. One expression,
/// compiled for the second reading — and a test that says so, the way the envelope and the walker
/// are kept honest.</para>
/// </summary>
public sealed class SharedForReadingTests
{
    private static readonly DivisionOptions Division = new()
    {
        Code = "IT",
        Locales = ["it", "en"],
        DefaultLocale = "it",
    };

    [Fact]
    public async Task ATemplateOfAnotherDepartmentIsReadable()
    {
        // A coordinator of events, and a template of the web team. Without this rule the seeded
        // templates are a tool of one department out of nine (design M1 §9.4).
        var handler = HandlerFor(new EffectivePermission(CorePermissions.ContentView, Department.ED, Role));

        Assert.True(await Allows(handler, Template(Department.WD), CorePermissions.ContentView));
    }

    [Fact]
    public async Task ATemplateOfAnotherDepartmentIsNotWritable()
    {
        // ⚠️ The test that fails when the rule stops being about reading. Holding `Content.Edit`
        // somewhere must not become holding it on a template belonging to somebody else: sharing a
        // row for reading is not a licence to change it.
        var handler = HandlerFor(
            new EffectivePermission(CorePermissions.ContentEdit, Department.ED, Role),
            new EffectivePermission(CorePermissions.ContentManageTemplates, Department.ED, Role));

        Assert.False(await Allows(handler, Template(Department.WD), CorePermissions.ContentEdit));
        Assert.False(await Allows(handler, Template(Department.WD), CorePermissions.ContentManageTemplates));

        // Their own, on the other hand, is theirs to change.
        Assert.True(await Allows(handler, Template(Department.ED), CorePermissions.ContentEdit));
    }

    [Fact]
    public async Task APageIsNotShared()
    {
        // Only the templates. A page of another department stays as unreadable as it was, which is
        // what makes this a narrow rule rather than a hole in the departmental model.
        var handler = HandlerFor(new EffectivePermission(CorePermissions.ContentView, Department.ED, Role));

        var page = new ContentEntry { OwnerDepartment = Department.WD, IsTemplate = false };

        Assert.False(await Allows(handler, page, CorePermissions.ContentView));
    }

    [Fact]
    public void TheSqlAndTheInMemoryHalvesAgree()
    {
        // One expression on the entity, compiled for the reading the handler does. The two are the
        // same source, and this is what says so out loud: if somebody ever writes the second half
        // by hand, this is where it stops.
        var compiled = ContentEntry.SharedForReading.Compile();

        // Every shape a row takes across the properties either half could plausibly look at, rather
        // than four rows chosen by hand. ⚠️ The first version of this test *was* four rows chosen by
        // hand, all of them at the default visibility, and it stayed green while a hand-written
        // second half disagreed on every real template — which is `Visibility.Staff` (implementation
        // plan M1 §A.10, found by breaking it).
        var rows =
            from isTemplate in new[] { true, false }
            from visibility in Enum.GetValues<Visibility>()
            from status in Enum.GetValues<PublishStatus>()
            from owner in new[] { Department.WD, Department.ED }
            select new ContentEntry
            {
                OwnerDepartment = owner,
                IsTemplate = isTemplate,
                Visibility = visibility,
                Status = status,
            };

        foreach (var row in rows)
        {
            Assert.Equal(compiled(row), ((ISharedForReading)row).IsSharedForReading);
        }
    }

    private const string Role = "role:test";

    private static ContentEntry Template(Department owner) =>
        new() { OwnerDepartment = owner, IsTemplate = true, Visibility = Visibility.Staff };

    private static DepartmentAuthorizationHandler HandlerFor(params EffectivePermission[] permissions)
    {
        var identity = HubClaims.BuildIdentity(
            vid: 600100,
            firstName: "Test",
            lastName: "User",
            locale: "it",
            securityStamp: "stamp",
            isSuperadmin: false,
            isStaff: true,
            positions: [],
            permissions: permissions);

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var currentUser = new HttpContextCurrentUser(
            new HttpContextAccessor { HttpContext = context },
            Options.Create(Division));

        // The real catalogue of a hub with no modules: "is this permission the one that reads?" is
        // its question to answer, and asking a double would be asking a copy.
        return new DepartmentAuthorizationHandler(currentUser, Options.Create(Division), PermissionCatalog.Core);
    }

    private static async Task<bool> Allows(
        DepartmentAuthorizationHandler handler,
        object resource,
        string permission)
    {
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity()),
            resource);

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }
}
