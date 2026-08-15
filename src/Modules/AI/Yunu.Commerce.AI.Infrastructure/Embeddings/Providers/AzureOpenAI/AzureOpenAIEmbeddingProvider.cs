using System.ClientModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Embeddings;
using Yunu.Commerce.AI.Application.Configuration;
using Yunu.Commerce.AI.Application.Embeddings;

namespace Yunu.Commerce.AI.Infrastructure.Embeddings.Providers.AzureOpenAI;

/// <summary>
/// Azure OpenAI adapter implementing <see cref="IEmbeddingProvider"/>
/// (docs/adr/0008-genai-provider-abstraction.md, "Azure OpenAI provider"). Uses the
/// official OpenAI SDK against the Azure OpenAI v1 preview endpoint
/// (https://{resource}.openai.azure.com/openai/v1/) with API Key authentication.
/// Resolves its endpoint/deployment/dimensions from the logical
/// "CategoryEmbedding" model registration via <see cref="IAIModelCatalog"/>
/// (docs task: "Intent/Query Rewriting") instead of a dedicated options type,
/// so it shares its Azure OpenAI connection with other logical models (e.g.
/// "IntentRewriter"). The <see cref="EmbeddingClient"/> is created once and
/// reused for the lifetime of this singleton adapter (docs §32, "Dependency
/// Injection").
/// </summary>
public sealed class AzureOpenAIEmbeddingProvider : IEmbeddingProvider
{
    public const string ProviderName = "azure";

    private readonly EmbeddingClient _embeddingClient;
    private readonly ResolvedAIModel _model;
    private readonly ILogger<AzureOpenAIEmbeddingProvider> _logger;

    public AzureOpenAIEmbeddingProvider(
        IAIModelCatalog modelCatalog,
        ILogger<AzureOpenAIEmbeddingProvider> logger)
    {
        _model = modelCatalog.Resolve(AIModelNames.CategoryEmbedding, AIModelType.Embedding);
        _logger = logger;

        var client = new OpenAIClient(
            new ApiKeyCredential(_model.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(_model.Endpoint) });

        _embeddingClient = client.GetEmbeddingClient(_model.DeploymentName);
    }

    public string Name => ProviderName;

    public async Task<EmbeddingResult> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Embedding generation requested for deployment {DeploymentName} with input length {InputLength}",
            _model.DeploymentName,
            text.Length);

        var stopwatch = Stopwatch.StartNew();

        ClientResult<OpenAIEmbedding> response;

        try
        {
            response = await _embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new EmbeddingGenerationException($"Azure OpenAI embedding generation failed: {ex.Message}");
        }

        var embedding = response.Value.ToFloats().ToArray();

        if (embedding.Length != _model.Dimensions)
        {
            throw new EmbeddingGenerationException(
                $"Expected {_model.Dimensions} embedding dimensions but Azure returned {embedding.Length}.");
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "Embedding generated successfully for deployment {DeploymentName} with {Dimensions} dimensions in {ElapsedMilliseconds}ms",
            _model.DeploymentName,
            embedding.Length,
            stopwatch.ElapsedMilliseconds);

        return new EmbeddingResult(Name, _model.DeploymentName, embedding);
    }
}
