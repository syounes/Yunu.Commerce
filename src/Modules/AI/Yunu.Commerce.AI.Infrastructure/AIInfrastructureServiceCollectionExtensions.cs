using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yunu.Commerce.AI.Application.Embeddings;
using Yunu.Commerce.AI.Infrastructure.Embeddings.Providers.AzureOpenAI;

namespace Yunu.Commerce.AI.Infrastructure;

/// <summary>
/// Composition entry point for the AI Infrastructure layer (docs §49).
/// Hosts call this extension to register adapters implementing the module's ports.
/// Registers the Azure OpenAI <see cref="IEmbeddingProvider"/> (docs task:
/// "AI Embeddings smoke test"). Adding another provider (Google, Ollama, ...)
/// only requires registering another <see cref="IEmbeddingProvider"/> here;
/// the Application layer's <see cref="EmbeddingOrchestrator"/> does not change.
/// </summary>
public static class AIInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAIInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AzureOpenAIEmbeddingOptions>()
            .Bind(configuration.GetSection("AI:Embeddings:Providers:Azure"))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AzureOpenAIEmbeddingOptions>, AzureOpenAIEmbeddingOptionsValidator>();

        services.AddSingleton<IEmbeddingProvider, AzureOpenAIEmbeddingProvider>();

        return services;
    }
}

