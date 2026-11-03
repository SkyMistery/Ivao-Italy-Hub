using IvaoHub.Core.Auth;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Data;
using IvaoHub.Core.Division;
using IvaoHub.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IvaoHub.UnitTests;

/// <summary>
/// What the grants screen refuses before anything is written, for the holes issue #319 (item 3) found: a VID of 0 next to a
/// position counted as no VID, a blank permission was never told it is required, and any number passed for the kind or the
/// effect.
/// </summary>
public sealed class GrantWriteRulesTests
{
    private static GrantWriteDto Position(string value) => new(
        Vid: null,
        Kind: GrantKind.Permission,
        Value: value,
        Department: Department.TD,
        Effect: GrantEffect.Grant,
        ExpiresAt: null,
        Reason: null,
        RowVersion: default,
        PositionDepartment: Department.TD,
        PositionLevels: [StaffLevel.Coordinator]);

    private static async Task<IReadOnlyList<(string Property, string Message)>> ErrorsAsync(GrantWriteDto grant)
    {
        // A context that is never opened: the one rule that reads the table asks about a VID given alone, and none is here.
        await using var database = new HubDbContext(new DbContextOptionsBuilder<HubDbContext>()
            .UseMySql("Server=localhost;Database=none;User ID=none;Password=none", new MariaDbServerVersion(HubDbContext.ServerVersion))
            .Options);
        var validator = new GrantWriteDtoValidator(new PermissionCatalog(CorePermissions.All), database, new SystemClock());

        var result = await validator.ValidateAsync(grant, TestContext.Current.CancellationToken);
        return [.. result.Errors.Select(error => (error.PropertyName, error.ErrorMessage))];
    }

    [Fact]
    public async Task AGrantToAPositionIsValid()
    {
        Assert.Empty(await ErrorsAsync(Position(CorePermissions.LinksEdit)));
    }

    [Fact]
    public async Task AVidOfZeroNextToAPositionIsASecondSubject()
    {
        Assert.Contains(("Vid", "errors.grant.subject"), await ErrorsAsync(Position(CorePermissions.LinksEdit) with { Vid = 0 }));
    }

    [Fact]
    public async Task ABlankPermissionIsRequired()
    {
        Assert.Contains(("Value", "errors.required"), await ErrorsAsync(Position(" ")));
    }

    [Fact]
    public async Task AKindOrAnEffectOutsideTheirValuesIsRefused()
    {
        var errors = await ErrorsAsync(Position(CorePermissions.LinksEdit) with { Kind = (GrantKind)42, Effect = (GrantEffect)42 });

        Assert.Contains(("Kind", "errors.required"), errors);
        Assert.Contains(("Effect", "errors.required"), errors);
    }
}
