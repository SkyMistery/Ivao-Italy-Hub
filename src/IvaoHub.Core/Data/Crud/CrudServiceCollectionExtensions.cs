using FluentValidation;
using IvaoHub.Core.Localization;
using IvaoHub.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IvaoHub.Core.Data.Crud;

/// <summary>
/// What the CRUD engine needs in the container. One call, so that a host cannot end up with the
/// engine mapped and its answers unregistered.
/// </summary>
public static class CrudServiceCollectionExtensions
{
    public static IServiceCollection AddHubCrud(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The language files, read by the server for the parts of an answer a person reads.
        services.TryAddSingleton<LocaleCatalog>();

        // Every write DTO of the core brings its validator with it; a module adds its own the same
        // way, from its own assembly.
        services.AddValidatorsFromAssemblyContaining<LocaleCatalog>(includeInternalTypes: true);

        services.AddProblemDetails();
        services.AddExceptionHandler<DomainExceptionHandler>();

        return services;
    }
}
