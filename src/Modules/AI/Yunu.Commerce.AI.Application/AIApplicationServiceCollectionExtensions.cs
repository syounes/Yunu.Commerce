using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yunu.Commerce.AI.Application.Embeddings;

namespace Yunu.Commerce.AI.Application;

/// <summary>
/// Composition entry point for the AI Application layer (docs §49).
/// Registers the provider-agnostic embedding orchestration. Vendor-specific
/// <see cref="IEmbeddingProvider"/> adapters are registered by Infrastructure.
/// </summary>
public static class AIApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAIApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmbeddingOptions>()
            .Bind(configuration.GetSection("AI:Embeddings"))
            .ValidateOnStart();

        services.AddSingleton<EmbeddingOrchestrator>();

        return services;
    }
}

