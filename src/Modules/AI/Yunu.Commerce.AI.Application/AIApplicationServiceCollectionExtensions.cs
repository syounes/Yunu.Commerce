using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yunu.Commerce.AI.Application.Configuration;
using Yunu.Commerce.AI.Application.Embeddings;
using Yunu.Commerce.AI.Application.Reranking;

namespace Yunu.Commerce.AI.Application;

/// <summary>
/// Composition entry point for the AI Application layer (docs §49).
/// Registers the provider-agnostic embedding orchestration and the logical AI
/// model catalog (docs task: "Intent/Query Rewriting"). Vendor-specific
/// <see cref="IEmbeddingProvider"/> and <see
/// cref="Yunu.Commerce.AI.Application.IntentRewriting.IIntentRewriter"/>
/// adapters are registered by Infrastructure.
/// </summary>
public static class AIApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAIApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmbeddingOptions>()
            .Bind(configuration.GetSection("AI:Embeddings"))
            .ValidateOnStart();

        services.AddOptions<AIOptions>()
            .Bind(configuration.GetSection("AI"))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AIOptions>, AIOptionsValidator>();

        services.AddSingleton<IAIModelCatalog, AIModelCatalog>();

        services.AddSingleton<EmbeddingOrchestrator>();

        services.AddOptions<RerankingOptions>()
            .Bind(configuration.GetSection("AI:Reranking"))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<RerankingOptions>, RerankingOptionsValidator>();

        return services;
    }
}

