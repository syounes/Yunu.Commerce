using Microsoft.Extensions.DependencyInjection;

namespace Yunu.Commerce.Catalog.Application;

/// <summary>
/// Composition entry point for the Catalog Application layer (docs §49).
/// Hosts call this extension to register use case handlers and application services.
/// No use case implementation is registered yet during the architecture skeleton phase.
/// </summary>
public static class CatalogApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        return services;
    }
}
