using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Yunu.Commerce.AI.Application.Configuration;
using Yunu.Commerce.AI.Application.IntentRewriting;

namespace Yunu.Commerce.AI.Infrastructure.IntentRewriting.AzureOpenAI;

/// <summary>
/// Azure OpenAI adapter implementing <see cref="IIntentRewriter"/> (docs task:
/// "Intent/Query Rewriting"), mirroring the structure of
/// <see cref="Yunu.Commerce.AI.Infrastructure.Embeddings.Providers.AzureOpenAI.AzureOpenAIEmbeddingProvider"/>.
/// Resolves its endpoint/deployment from the logical "IntentRewriter" model
/// registration via <see cref="IAIModelCatalog"/>, uses Structured Outputs
/// (strict JSON Schema) so the response is always deterministic and
/// deserializable, and never performs retrieval, tool calling or database
/// access. The <see cref="ChatClient"/> is created once and reused for the
/// lifetime of this singleton adapter (docs §32, "Dependency Injection").
/// </summary>
public sealed class AzureOpenAIIntentRewriter : IIntentRewriter
{
    private static readonly ChatCompletionOptions CompletionOptionsTemplate = new()
    {
        Temperature = 0.1f,
        MaxOutputTokenCount = 800,
        ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            IntentRewriteJsonSchema.SchemaName,
            IntentRewriteJsonSchema.Build(),
            jsonSchemaIsStrict: true)
    };

    private readonly ChatClient _chatClient;
    private readonly ResolvedAIModel _model;
    private readonly ILogger<AzureOpenAIIntentRewriter> _logger;

    public AzureOpenAIIntentRewriter(
        IAIModelCatalog modelCatalog,
        ILogger<AzureOpenAIIntentRewriter> logger)
    {
        _model = modelCatalog.Resolve(AIModelNames.IntentRewriter, AIModelType.Chat);
        _logger = logger;

        var client = new OpenAIClient(
            new ApiKeyCredential(_model.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(_model.Endpoint) });

        _chatClient = client.GetChatClient(_model.DeploymentName);
    }

    public async Task<IntentRewriteResult> RewriteAsync(
        IntentRewriteRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Intent rewrite requested for deployment {DeploymentName} with input length {InputLength} and locale {Locale}",
            _model.DeploymentName,
            request.Input.Length,
            request.Locale);

        var messages = new ChatMessage[]
        {
            new SystemChatMessage(IntentRewriterSystemPrompt.Text),
            new UserChatMessage(BuildUserMessage(request))
        };

        var stopwatch = Stopwatch.StartNew();

        ClientResult<ChatCompletion> response;

        try
        {
            response = await _chatClient.CompleteChatAsync(messages, CompletionOptionsTemplate, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ClientResultException ex)
        {
            throw MapClientResultException(ex);
        }
        catch (Exception ex)
        {
            throw new IntentRewriteException(
                IntentRewriteFailureReason.ProviderUnavailable,
                $"Azure OpenAI intent rewrite failed: {ex.Message}");
        }

        stopwatch.Stop();

        var completion = response.Value;

        if (completion.FinishReason == ChatFinishReason.ContentFilter)
        {
            _logger.LogWarning(
                "Intent rewrite content filtered for deployment {DeploymentName}",
                _model.DeploymentName);

            throw new IntentRewriteException(
                IntentRewriteFailureReason.ContentFiltered,
                "The Azure OpenAI content filter blocked this request.");
        }

        var rawJson = completion.Content.Count > 0 ? completion.Content[0].Text : null;

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new IntentRewriteException(
                IntentRewriteFailureReason.InvalidResponse,
                "Azure OpenAI returned an empty intent rewrite response.");
        }

        IntentRewriteModelResponse? modelResponse;

        try
        {
            modelResponse = JsonSerializer.Deserialize<IntentRewriteModelResponse>(rawJson);
        }
        catch (JsonException ex)
        {
            throw new IntentRewriteException(
                IntentRewriteFailureReason.InvalidResponse,
                $"Azure OpenAI intent rewrite response could not be parsed: {ex.Message}");
        }

        if (modelResponse is null)
        {
            throw new IntentRewriteException(
                IntentRewriteFailureReason.InvalidResponse,
                "Azure OpenAI intent rewrite response deserialized to null.");
        }

        if (!Enum.TryParse<CatalogIntent>(modelResponse.Intent, ignoreCase: true, out var intent))
        {
            intent = CatalogIntent.Unknown;
        }

        _logger.LogInformation(
            "Intent rewrite completed for deployment {DeploymentName} with intent {Intent} in {ElapsedMilliseconds}ms",
            _model.DeploymentName,
            intent,
            stopwatch.ElapsedMilliseconds);

        return new IntentRewriteResult(
            OriginalInput: request.Input,
            NormalizedQuery: modelResponse.NormalizedQuery,
            SemanticQuery: modelResponse.SemanticQuery,
            Intent: intent,
            DetectedLanguage: modelResponse.DetectedLanguage,
            TargetLocale: request.Locale,
            CategoryHint: modelResponse.CategoryHint,
            AttributeHints: modelResponse.AttributeHints
                .Select(h => new AttributeHint(h.RawName, h.RawValue))
                .ToArray(),
            SearchTerms: modelResponse.SearchTerms.ToArray(),
            Confidence: Math.Clamp(modelResponse.Confidence, 0m, 1m));
    }

    private static string BuildUserMessage(IntentRewriteRequest request)
    {
        return $"""
            Locale alvo: {request.Locale}
            Entrada do usuário: {request.Input}
            """;
    }

    private static IntentRewriteException MapClientResultException(ClientResultException ex)
    {
        var reason = ex.Status switch
        {
            401 or 403 => IntentRewriteFailureReason.Authentication,
            429 => IntentRewriteFailureReason.RateLimited,
            408 => IntentRewriteFailureReason.Timeout,
            >= 500 => IntentRewriteFailureReason.ProviderUnavailable,
            _ => IntentRewriteFailureReason.InvalidResponse
        };

        return new IntentRewriteException(reason, $"Azure OpenAI intent rewrite failed: {ex.Message}");
    }
}
