using Microsoft.Extensions.DependencyInjection;

namespace Yunu.Commerce.AI.Application;

/// <summary>
/// Composition entry point for the AI Application layer (docs §49).
/// Hosts call this extension to register use case handlers and application services.
/// No use case implementation is registered yet during the architecture skeleton phase.
/// </summary>
public static class AIApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAIApplication(this IServiceCollection services)
    {
        return services;
    }
}
