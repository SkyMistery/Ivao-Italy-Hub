using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation;
using IvaoHub.Core.Auth;
using IvaoHub.Core.Data;
using IvaoHub.Core.Data.Crud;
using IvaoHub.Core.Division;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IvaoHub.Core.Modules;

/// <summary>
/// The settings of a module that its department changes from the interface — the daily limit of legs,
/// the two numbers of the estimated time of the tours — declared by the module and kept by the core
/// (decided with Carmine, 16 September 2026, phase T5 of M2; note 2026-09-16-impostazioni-dei-moduli).
/// <para>One row of <c>hub_division_settings</c> per module, <c>modules.{key}.settings</c>, audited like every
/// row of that table. A value never written is the module's default, and a property the module adds in a
/// later release starts at its default on an installation that saved the others long ago.</para>
/// </summary>
public abstract class ModuleSettingsDescriptor
{
    private protected ModuleSettingsDescriptor(string permission) => Permission = permission;

    /// <summary>
    /// Who may read and change them: a permission of the module, held on the module's base department
    /// (<c>Tours.ManageSettings</c>), or anywhere for a module that has no base department.
    /// </summary>
    public string Permission { get; }

    internal abstract Type SettingsType { get; }

    internal abstract object Defaults { get; }

    internal abstract Task<FluentValidation.Results.ValidationResult> ValidateAsync(
        object settings,
        IServiceProvider services,
        CancellationToken cancellationToken);

    /// <summary>The settings of a module: their type, their values before anybody chose, their rules.</summary>
    public static ModuleSettingsDescriptor Create<TSettings, TValidator>(string permission, TSettings defaults)
        where TSettings : class
        where TValidator : IValidator<TSettings> =>
        new ModuleSettingsDescriptor<TSettings, TValidator>(permission, defaults);
}

internal sealed class ModuleSettingsDescriptor<TSettings, TValidator>(string permission, TSettings defaults)
    : ModuleSettingsDescriptor(permission)
    where TSettings : class
    where TValidator : IValidator<TSettings>
{
    internal override Type SettingsType => typeof(TSettings);

    internal override object Defaults => defaults;

    internal override Task<FluentValidation.Results.ValidationResult> ValidateAsync(
        object settings,
        IServiceProvider services,
        CancellationToken cancellationToken) =>
        ActivatorUtilities.CreateInstance<TValidator>(services).ValidateAsync((TSettings)settings, cancellationToken);
}

/// <summary>Reads and writes the settings of a module. What a module's own code asks for its values.</summary>
public sealed class ModuleSettingsStore(
    HubDbContext database,
    ModuleRegistry modules,
    IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json,
    IClock clock)
{
    /// <summary>How the settings of a module are keyed in <c>hub_division_settings</c>.</summary>
    public static string SettingsKey(string moduleKey) => $"modules.{moduleKey}.settings";

    private JsonSerializerOptions Json => json.Value.SerializerOptions;

    /// <summary>The settings as they stand: what was saved, over the defaults.</summary>
    public async Task<TSettings> GetAsync<TSettings>(string moduleKey, CancellationToken cancellationToken = default)
        where TSettings : class =>
        (TSettings)(await ReadAsync(Descriptor(moduleKey), moduleKey, cancellationToken)).Value;

    internal async Task<(object Value, JsonNode Node)> ReadAsync(
        ModuleSettingsDescriptor descriptor,
        string moduleKey,
        CancellationToken cancellationToken)
    {
        var stored = await database.DivisionSettings
            .AsNoTracking()
            .Where(row => row.Key == SettingsKey(moduleKey))
            .Select(row => row.ValueJson)
            .FirstOrDefaultAsync(cancellationToken);

        var node = JsonSerializer.SerializeToNode(descriptor.Defaults, descriptor.SettingsType, Json)!.AsObject();

        if (stored is not null && JsonNode.Parse(stored) is JsonObject saved)
        {
            // Property by property, so a setting added in a later release keeps its default.
            foreach (var (name, value) in saved)
            {
                if (node.ContainsKey(name))
                {
                    node[name] = value?.DeepClone();
                }
            }
        }

        return (node.Deserialize(descriptor.SettingsType, Json)!, node);
    }

    internal async Task<JsonNode> WriteAsync(
        ModuleSettingsDescriptor descriptor,
        string moduleKey,
        object settings,
        int vid,
        CancellationToken cancellationToken)
    {
        var node = JsonSerializer.SerializeToNode(settings, descriptor.SettingsType, Json)!;
        var key = SettingsKey(moduleKey);

        var row = await database.DivisionSettings.FirstOrDefaultAsync(setting => setting.Key == key, cancellationToken);
        if (row is null)
        {
            row = new DivisionSetting { Key = key };
            database.DivisionSettings.Add(row);
        }

        row.ValueJson = node.ToJsonString(Json);
        row.UpdatedBy = vid;
        row.UpdatedAt = clock.UtcNow;

        // Audited by the interceptor, because DivisionSetting is [Audited].
        await database.SaveChangesAsync(cancellationToken);
        return node;
    }

    private ModuleSettingsDescriptor Descriptor(string moduleKey) =>
        modules.Find(moduleKey)?.Settings
        ?? throw new InvalidOperationException($"The module '{moduleKey}' declares no settings.");
}

/// <summary>
/// <c>/api/modules/{key}/settings</c>: the form of a module's settings reads and saves here. The value is
/// the module's own JSON, typed by the module's schema on both sides; the core checks the permission,
/// runs the module's validator and stores it.
/// </summary>
public static class ModuleSettingsEndpoints
{
    public const string Pattern = "/api/modules/{key}/settings";

    /// <summary>A body that is not the shape of the settings at all: a text where a number goes.</summary>
    public const string InvalidKey = "errors.settings.invalid";

    public static IEndpointRouteBuilder MapModuleSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Pattern, async Task<Results<Ok<JsonNode>, NotFound, ForbidHttpResult>> (
            string key,
            ModuleRegistry modules,
            ModuleSettingsStore store,
            ICurrentUser currentUser,
            CancellationToken cancellationToken) =>
        {
            if (modules.Find(key)?.Settings is not { } descriptor)
            {
                return TypedResults.NotFound();
            }

            if (!MayManage(descriptor, key, modules, currentUser))
            {
                return TypedResults.Forbid();
            }

            return TypedResults.Ok((await store.ReadAsync(descriptor, key, cancellationToken)).Node);
        })
        .WithName("ModuleSettings")
        .RequireAuthorization(Auth.Permissions.HubPolicies.SignedIn);

        app.MapPut(Pattern, async Task<Results<Ok<JsonNode>, NotFound, ForbidHttpResult, ValidationProblem>> (
            HttpContext http,
            string key,
            JsonElement body,
            ModuleRegistry modules,
            ModuleSettingsStore store,
            ICurrentUser currentUser,
            IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json,
            LocaleCatalog catalog) =>
        {
            if (modules.Find(key)?.Settings is not { } descriptor)
            {
                return TypedResults.NotFound();
            }

            if (!MayManage(descriptor, key, modules, currentUser))
            {
                return TypedResults.Forbid();
            }

            object? settings;
            try
            {
                settings = body.Deserialize(descriptor.SettingsType, json.Value.SerializerOptions);
            }
            catch (JsonException)
            {
                settings = null;
            }

            if (settings is null)
            {
                return TypedResults.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal) { [string.Empty] = [InvalidKey] },
                    title: catalog.Resolve(currentUser.Locale, CrudProblems.ValidationTitleKey));
            }

            var result = await descriptor.ValidateAsync(settings, http.RequestServices, http.RequestAborted);
            if (!result.IsValid)
            {
                return TypedResults.ValidationProblem(
                    result.Errors
                        .GroupBy(failure => CrudProblems.FieldName(failure.PropertyName), StringComparer.Ordinal)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                            StringComparer.Ordinal),
                    title: catalog.Resolve(currentUser.Locale, CrudProblems.ValidationTitleKey));
            }

            return TypedResults.Ok(await store.WriteAsync(descriptor, key, settings, currentUser.Vid, http.RequestAborted));
        })
        .WithName("ModuleSetSettings")
        .RequireAuthorization(Auth.Permissions.HubPolicies.SignedIn);

        return app;
    }

    private static bool MayManage(ModuleSettingsDescriptor descriptor, string key, ModuleRegistry modules, ICurrentUser currentUser) =>
        modules.BaseDepartmentOf(key) is { } baseDepartment
            ? currentUser.Has(descriptor.Permission, baseDepartment)
            : currentUser.HasAny(descriptor.Permission);
}
