using Microsoft.Extensions.DependencyInjection;

namespace Yunu.Commerce.Pricing.Application;

/// <summary>
/// Composition entry point for the Pricing Application layer (docs §49).
/// Hosts call this extension to register use case handlers and application services.
/// No use case implementation is registered yet during the architecture skeleton phase.
/// </summary>
public static class PricingApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddPricingApplication(this IServiceCollection services)
    {
        return services;
    }
}
