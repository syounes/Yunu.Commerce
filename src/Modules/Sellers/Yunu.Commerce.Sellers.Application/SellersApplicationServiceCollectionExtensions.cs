using Microsoft.Extensions.DependencyInjection;

namespace Yunu.Commerce.Sellers.Application;

/// <summary>
/// Composition entry point for the Sellers Application layer (docs §49).
/// Hosts call this extension to register use case handlers and application services.
/// No use case implementation is registered yet during the architecture skeleton phase.
/// </summary>
public static class SellersApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddSellersApplication(this IServiceCollection services)
    {
        return services;
    }
}
