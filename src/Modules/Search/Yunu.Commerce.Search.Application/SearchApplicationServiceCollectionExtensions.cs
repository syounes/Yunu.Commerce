using Microsoft.Extensions.DependencyInjection;

namespace Yunu.Commerce.Search.Application;

/// <summary>
/// Composition entry point for the Search Application layer (docs §49).
/// Hosts call this extension to register use case handlers and application services.
/// No use case implementation is registered yet during the architecture skeleton phase.
/// </summary>
public static class SearchApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddSearchApplication(this IServiceCollection services)
    {
        return services;
    }
}
