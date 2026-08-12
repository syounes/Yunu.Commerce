using Microsoft.Extensions.DependencyInjection;

namespace Yunu.Commerce.Availability.Application;

/// <summary>
/// Composition entry point for the Availability Application layer (docs §49).
/// Hosts call this extension to register use case handlers and application services.
/// No use case implementation is registered yet during the architecture skeleton phase.
/// </summary>
public static class AvailabilityApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAvailabilityApplication(this IServiceCollection services)
    {
        return services;
    }
}
