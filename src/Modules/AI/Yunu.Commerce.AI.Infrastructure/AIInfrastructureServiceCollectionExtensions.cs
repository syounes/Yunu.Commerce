using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Yunu.Commerce.AI.Infrastructure;

/// <summary>
/// Composition entry point for the AI Infrastructure layer (docs §49).
/// Hosts call this extension to register adapters implementing the module's ports.
/// No persistence, messaging or external provider adapter is wired yet during the
/// architecture skeleton phase (docs/architecture/06-solution-structure.md §55-56).
/// </summary>
public static class AIInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAIInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
