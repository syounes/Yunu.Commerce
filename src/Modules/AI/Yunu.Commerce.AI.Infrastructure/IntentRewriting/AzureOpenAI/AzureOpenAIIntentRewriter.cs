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
        MaxOutputTokenCount = 2000,
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
        : this(modelCatalog, logger, transport: null)
    {
    }

    /// <summary>
    /// Test-only constructor allowing a fake <see cref="System.ClientModel.Primitives.PipelineTransport"/>
    /// to be injected so unit tests can simulate provider responses (e.g.
    /// <c>finish_reason = length</c>) without calling the real Azure OpenAI
    /// endpoint. Not intended for production DI registration.
    /// </summary>
    internal AzureOpenAIIntentRewriter(
        IAIModelCatalog modelCatalog,
        ILogger<AzureOpenAIIntentRewriter> logger,
        System.ClientModel.Primitives.PipelineTransport? transport)
    {
        _model = modelCatalog.Resolve(AIModelNames.IntentRewriter, AIModelType.Chat);
        _logger = logger;

        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(_model.Endpoint) };

        if (transport is not null)
        {
            clientOptions.Transport = transport;
        }

        var client = new OpenAIClient(new ApiKeyCredential(_model.ApiKey), clientOptions);

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

        if (completion.FinishReason == ChatFinishReason.Length)
        {
            var truncatedResponseLength = completion.Content.Count > 0 ? completion.Content[0].Text?.Length : 0;

            _logger.LogWarning(
                "Intent rewrite output truncated for deployment {DeploymentName}: FinishReason={FinishReason}, " +
                "InputTokenCount={InputTokenCount}, OutputTokenCount={OutputTokenCount}, " +
                "ResponseLength={ResponseLength}, Provider={Provider}, TraceId={TraceId}",
                _model.DeploymentName,
                completion.FinishReason,
                completion.Usage?.InputTokenCount,
                completion.Usage?.OutputTokenCount,
                truncatedResponseLength,
                "AzureOpenAI",
                System.Diagnostics.Activity.Current?.Id);

            throw new IntentRewriteException(
                IntentRewriteFailureReason.OutputTruncated,
                "Azure OpenAI intent rewrite response was truncated because it reached the maximum output token limit.");
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
            _logger.LogWarning(
                "Intent rewrite response could not be parsed for deployment {DeploymentName}: " +
                "LineNumber={LineNumber}, BytePositionInLine={BytePositionInLine}, " +
                "ResponseLength={ResponseLength}, FinishReason={FinishReason}",
                _model.DeploymentName,
                ex.LineNumber,
                ex.BytePositionInLine,
                rawJson.Length,
                completion.FinishReason);

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
            "Intent rewrite completed for deployment {DeploymentName} with intent {Intent} in {ElapsedMilliseconds}ms, " +
            "FinishReason={FinishReason}, InputTokenCount={InputTokenCount}, OutputTokenCount={OutputTokenCount}, " +
            "ResponseLength={ResponseLength}, Provider={Provider}, TraceId={TraceId}",
            _model.DeploymentName,
            intent,
            stopwatch.ElapsedMilliseconds,
            completion.FinishReason,
            completion.Usage?.InputTokenCount,
            completion.Usage?.OutputTokenCount,
            rawJson.Length,
            "AzureOpenAI",
            System.Diagnostics.Activity.Current?.Id);

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
            Confidence: Math.Clamp(modelResponse.Confidence, 0m, 1m),
            CategorySearchQuery: modelResponse.CategorySearchQuery);
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
