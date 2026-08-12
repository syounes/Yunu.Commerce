using Microsoft.Extensions.DependencyInjection;

namespace Yunu.Commerce.Fulfillment.Application;

/// <summary>
/// Composition entry point for the Fulfillment Application layer (docs §49).
/// Hosts call this extension to register use case handlers and application services.
/// No use case implementation is registered yet during the architecture skeleton phase.
/// </summary>
public static class FulfillmentApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddFulfillmentApplication(this IServiceCollection services)
    {
        return services;
    }
}
