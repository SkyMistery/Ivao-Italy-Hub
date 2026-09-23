using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using IvaoHub.Core.Auth.Permissions;
using IvaoHub.Core.Content;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Data;
using IvaoHub.Core.Modules;
using IvaoHub.Core.Preferences;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IvaoHub.IntegrationTests;

/// <summary>
/// A module that exists only in the test host. It took over from the ATC module on 13 September
/// 2026, when that one left the build together with vIPI (note 2026-09-13-staccarsi-da-vipi): the
/// build has no module of its own until events opens M2, and the composition — menu, endpoints,
/// fallback exclusions, maintenance, the catalogue of permissions — still has to be proved against
/// the real host rather than against a registry built by hand.
/// <para>It is added through <see cref="ModuleServiceCollectionExtensions.AddHubModule"/>, the same
/// call the application makes for every module of its list, so what is proved is the application's
/// own path and not a second one written for tests.</para>
/// </summary>
public sealed class SampleModule : ModuleBase
{
    public const string ModuleKey = "sample";

    /// <summary>The permission area of the module's table (M2): <c>Sample.View</c> and <c>Sample.Edit</c>.</summary>
    public const string PermissionArea = "Sample";

    public const string ViewPermission = "Sample.View";

    public const string EditPermission = "Sample.Edit";

    /// <summary>
    /// What a PIREP's validation will be: a permission that can be granted on a single row, and
    /// that the member the row is about may never use on it — super administrator included
    /// (M2, T3).
    /// </summary>
    public const string DecidePermission = "Sample.Decide";

    /// <summary>The rows in the care of several departments, through the generic CRUD engine.</summary>
    public const string ItemsPattern = "/api/sample/items";

    /// <summary>The same rows read through the global query filter, as a public page of the module would.</summary>
    public const string VisiblePattern = "/api/sample/visible";

    /// <summary>
    /// Deciding one row, the way a validator decides one report: the permission is checked
    /// <b>against the row</b>, so the scope of the row and the member it is about both count.
    /// </summary>
    public const string DecidePattern = "/api/sample/items/{id:long}/decide";

    /// <summary>A permission of the module, so that the catalogue is seen to compose it.</summary>
    public const string ReadPermission = "Sample.Read";

    public const string NavigationKey = "nav.sample";

    public const string NavigationPath = "/sample";

    /// <summary>Something behind the same host that is not the single page application.</summary>
    public const string Exclusion = "/sample-legacy";

    public override string Key => ModuleKey;

    public override IReadOnlyList<PermissionDescriptor> Permissions =>
    [
        new PermissionDescriptor(ReadPermission, IsGlobal: true),
        new PermissionDescriptor(ViewPermission, IsGlobal: false),
        new PermissionDescriptor(EditPermission, IsGlobal: false),
        new PermissionDescriptor(DecidePermission, IsGlobal: false, DeniedToStakeholder: true),
    ];

    public override IEnumerable<Type> DbContextTypes => [typeof(SampleDbContext)];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SampleDbContext>(ModuleKey);

        // What a thread may cite of this module (M2, T14): how a tour will say what a report is.
        services.AddScoped<IContactReferenceResolver, SampleReferenceResolver>();
    }

    public override IReadOnlyList<NavItemDescriptor> PublicNavigation =>
        [new NavItemDescriptor(NavigationKey, NavigationPath)];

    /// <summary>An entry of the back office, behind a permission of the module (M2: its own section).</summary>
    public const string StaffNavigationKey = "sample.nav.items";

    public const string StaffNavigationPath = "/staff/sample";

    public override IReadOnlyList<NavItemDescriptor> StaffNavigation =>
        [new NavItemDescriptor(StaffNavigationKey, StaffNavigationPath, ViewPermission)];

    public override IReadOnlyList<string> SpaFallbackExclusions => [Exclusion];

    /// <summary>A preference of the module, the shape the order of the validation queue will have (T4b).</summary>
    public const string OrderPreference = "sample.order";

    public override IReadOnlyList<PreferenceDescriptor> Preferences =>
        [PreferenceDescriptor.OneOf(OrderPreference, "date", "tour")];

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Anonymous, and it says only that the module is mapped: a read that maintenance leaves
        // open, and a POST to the same address that maintenance closes.
        endpoints.MapGroup($"/api/{ModuleKey}")
            .MapGet("/ping", () => TypedResults.Ok(new SamplePing(ModuleKey)));

        // A table of the module through the generic engine, exactly as a real module maps one.
        endpoints.MapCrud<SampleItem, SampleItemDto, SampleItemDto, SampleItemWriteDto>(ItemsPattern, options =>
        {
            options.PermissionArea = PermissionArea;
            options.ContextType = typeof(SampleDbContext);
            options.DefaultOrder = item => item.Id;
            options.ToList = SampleItemMapping.ToDto;
            options.ToDetail = SampleItemMapping.ToDto;
            options.Apply = SampleItemMapping.Apply;
        });

        // And through the filter: what an anonymous page of the module would list.
        endpoints.MapGet(VisiblePattern, async (SampleDbContext database, CancellationToken cancellationToken) =>
                TypedResults.Ok(await database.Items.Select(item => item.Id).ToListAsync(cancellationToken)))
            .AllowAnonymous();

        // Deciding one row. The permission is asked of the single handler **with the row in hand**,
        // which is what makes the scope of the row and the member it is about count — the shape a
        // validator taking a PIREP will have (M2, T3).
        endpoints.MapPost(DecidePattern, async Task<IResult> (
            long id,
            SampleDbContext database,
            IAuthorizationService authorization,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            var item = await database.Items
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

            if (item is null)
            {
                return Results.NotFound();
            }

            var allowed = await authorization.AuthorizeAsync(principal, item, DecidePermission);
            return allowed.Succeeded ? Results.Ok(SampleItemMapping.ToDto(item)) : Results.Forbid();
        });
    }
}

public sealed record SamplePing(string Module);
