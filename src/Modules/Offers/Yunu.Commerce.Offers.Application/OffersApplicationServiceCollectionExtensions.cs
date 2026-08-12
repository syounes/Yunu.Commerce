using Microsoft.Extensions.DependencyInjection;

namespace Yunu.Commerce.Offers.Application;

/// <summary>
/// Composition entry point for the Offers Application layer (docs §49).
/// Hosts call this extension to register use case handlers and application services.
/// No use case implementation is registered yet during the architecture skeleton phase.
/// </summary>
public static class OffersApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddOffersApplication(this IServiceCollection services)
    {
        return services;
    }
}
