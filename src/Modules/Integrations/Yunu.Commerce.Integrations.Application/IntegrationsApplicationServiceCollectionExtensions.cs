using Microsoft.Extensions.DependencyInjection;

namespace Yunu.Commerce.Integrations.Application;

/// <summary>
/// Composition entry point for the Integrations Application layer (docs §49).
/// Hosts call this extension to register use case handlers and application services.
/// No use case implementation is registered yet during the architecture skeleton phase.
/// </summary>
public static class IntegrationsApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrationsApplication(this IServiceCollection services)
    {
        return services;
    }
}
