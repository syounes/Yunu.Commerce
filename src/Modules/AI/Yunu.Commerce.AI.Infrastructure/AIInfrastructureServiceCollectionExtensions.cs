using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yunu.Commerce.AI.Application.Embeddings;
using Yunu.Commerce.AI.Application.IntentRewriting;
using Yunu.Commerce.AI.Infrastructure.Embeddings.Providers.AzureOpenAI;
using Yunu.Commerce.AI.Infrastructure.IntentRewriting.AzureOpenAI;

namespace Yunu.Commerce.AI.Infrastructure;

/// <summary>
/// Composition entry point for the AI Infrastructure layer (docs §49).
/// Hosts call this extension to register adapters implementing the module's ports.
/// Registers the Azure OpenAI <see cref="IEmbeddingProvider"/> (docs task:
/// "AI Embeddings smoke test") and the Azure OpenAI <see cref="IIntentRewriter"/>
/// (docs task: "Intent/Query Rewriting"). Both resolve their endpoint,
/// credential and deployment from the shared logical model catalog
/// (<see cref="Yunu.Commerce.AI.Application.Configuration.IAIModelCatalog"/>)
/// registered by AI.Application, so adding another provider only requires
/// registering another adapter here; the Application layer's orchestration
/// types do not change.
/// </summary>
public static class AIInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAIInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IEmbeddingProvider, AzureOpenAIEmbeddingProvider>();
        services.AddSingleton<IIntentRewriter, AzureOpenAIIntentRewriter>();

        return services;
    }
}

