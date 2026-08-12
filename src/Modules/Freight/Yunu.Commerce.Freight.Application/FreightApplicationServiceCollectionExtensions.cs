using Microsoft.Extensions.DependencyInjection;

namespace Yunu.Commerce.Freight.Application;

/// <summary>
/// Composition entry point for the Freight Application layer (docs §49).
/// Hosts call this extension to register use case handlers and application services.
/// No use case implementation is registered yet during the architecture skeleton phase.
/// </summary>
public static class FreightApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddFreightApplication(this IServiceCollection services)
    {
        return services;
    }
}
