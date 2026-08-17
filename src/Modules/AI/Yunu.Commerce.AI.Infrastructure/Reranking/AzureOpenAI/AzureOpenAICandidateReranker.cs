using System.ClientModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Yunu.Commerce.AI.Application.Configuration;
using Yunu.Commerce.AI.Application.Reranking;

namespace Yunu.Commerce.AI.Infrastructure.Reranking.AzureOpenAI;

/// <summary>
/// Azure OpenAI adapter implementing <see cref="ICandidateReranker"/> (docs
/// task: "Contextual candidate reranking"), mirroring the structure of
/// <see cref="Yunu.Commerce.AI.Infrastructure.IntentRewriting.AzureOpenAI.AzureOpenAIIntentRewriter"/>.
/// Resolves its endpoint/deployment from the logical "CatalogReranker" model
/// registration via <see cref="IAIModelCatalog"/>, uses Structured Outputs
/// (strict JSON Schema) so the response is always deterministic and
/// deserializable, and never performs retrieval, tool calling or database
/// access. Every candidate index returned by the provider is re-validated
/// against the original request before being trusted (docs restriction:
/// "Nunca confie diretamente em IDs produzidos pelo LLM" applies equally to
/// indices): an inconsistent response is treated as a technical failure
/// (<see cref="CandidateRerankFailureReason.InvalidResponse"/>), never as a
/// selection. The <see cref="ChatClient"/> is created once and reused for the
/// lifetime of this singleton adapter (docs §32, "Dependency Injection").
/// </summary>
public sealed class AzureOpenAICandidateReranker : ICandidateReranker
{
    private static readonly ChatCompletionOptions CompletionOptionsTemplate = new()
    {
        // Lowest variability supported by the SDK/model for this specific
        // request (docs task: "Google Category reranking hardening" §
        // Determinism). This only affects the reranker's own request, not
        // any other Azure OpenAI call (Intent Rewriter uses its own,
        // separately configured ChatCompletionOptions). Absolute
        // determinism is never guaranteed by the provider; repeatability is
        // validated by controlled tests instead.
        Temperature = 0f,
        MaxOutputTokenCount = 800,
        ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            CandidateRerankJsonSchema.SchemaName,
            CandidateRerankJsonSchema.Build(),
            jsonSchemaIsStrict: true)
    };

    private readonly ChatClient _chatClient;
    private readonly ResolvedAIModel _model;
    private readonly ILogger<AzureOpenAICandidateReranker> _logger;

    public AzureOpenAICandidateReranker(
        IAIModelCatalog modelCatalog,
        ILogger<AzureOpenAICandidateReranker> logger)
    {
        _model = modelCatalog.Resolve(AIModelNames.CatalogReranker, AIModelType.Chat);
        _logger = logger;

        var client = new OpenAIClient(
            new ApiKeyCredential(_model.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(_model.Endpoint) });

        _chatClient = client.GetChatClient(_model.DeploymentName);
    }

    public async Task<CandidateRerankResult> RerankAsync(
        CandidateRerankRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Candidates.Count == 0)
        {
            throw new CandidateRerankException(
                CandidateRerankFailureReason.InvalidResponse,
                "Cannot rerank an empty candidate list.");
        }

        _logger.LogInformation(
            "Candidate reranking requested for deployment {DeploymentName} with task {Task} and {CandidateCount} candidates",
            _model.DeploymentName,
            request.Task,
            request.Candidates.Count);

        var messages = new ChatMessage[]
        {
            new SystemChatMessage(CandidateRerankerSystemPrompt.Text),
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
            throw new CandidateRerankException(
                CandidateRerankFailureReason.ProviderUnavailable,
                $"Azure OpenAI candidate reranking failed: {ex.Message}");
        }

        stopwatch.Stop();

        var completion = response.Value;

        if (completion.FinishReason == ChatFinishReason.ContentFilter)
        {
            _logger.LogWarning(
                "Candidate reranking content filtered for deployment {DeploymentName}",
                _model.DeploymentName);

            throw new CandidateRerankException(
                CandidateRerankFailureReason.ContentFiltered,
                "The Azure OpenAI content filter blocked this request.");
        }

        var rawJson = completion.Content.Count > 0 ? completion.Content[0].Text : null;

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new CandidateRerankException(
                CandidateRerankFailureReason.InvalidResponse,
                "Azure OpenAI returned an empty candidate reranking response.");
        }

        CandidateRerankModelResponse? modelResponse;

        try
        {
            modelResponse = JsonSerializer.Deserialize<CandidateRerankModelResponse>(rawJson);
        }
        catch (JsonException ex)
        {
            throw new CandidateRerankException(
                CandidateRerankFailureReason.InvalidResponse,
                $"Azure OpenAI candidate reranking response could not be parsed: {ex.Message}");
        }

        if (modelResponse is null)
        {
            throw new CandidateRerankException(
                CandidateRerankFailureReason.InvalidResponse,
                "Azure OpenAI candidate reranking response deserialized to null.");
        }

        var result = ValidateAndBuildResult(modelResponse, request);

        _logger.LogInformation(
            "Candidate reranking completed for deployment {DeploymentName} with decision {Decision} " +
            "confidence {Confidence} in {ElapsedMilliseconds}ms",
            _model.DeploymentName,
            result.Decision,
            result.Confidence,
            stopwatch.ElapsedMilliseconds);

        return result;
    }

    /// <summary>
    /// Validates the raw model response against the original candidate list
    /// and converts it into a trusted <see cref="CandidateRerankResult"/>.
    /// Any inconsistency (unknown, negative, duplicated or
    /// decision-incompatible index) is treated as an invalid provider
    /// response rather than silently corrected or ignored, per docs
    /// restriction: never accept an out-of-list index.
    /// </summary>
    private static CandidateRerankResult ValidateAndBuildResult(
        CandidateRerankModelResponse modelResponse,
        CandidateRerankRequest request)
    {
        if (!Enum.TryParse<CandidateRerankDecision>(modelResponse.Decision, ignoreCase: true, out var decision))
        {
            throw new CandidateRerankException(
                CandidateRerankFailureReason.InvalidResponse,
                $"Azure OpenAI candidate reranking returned an unknown decision '{modelResponse.Decision}'.");
        }

        var validIndices = request.Candidates.Select(c => c.Index).ToHashSet();

        var seenRankingIndices = new HashSet<int>();
        var ranking = new List<RerankedCandidateScore>(modelResponse.Ranking.Count);

        foreach (var entry in modelResponse.Ranking)
        {
            if (entry.CandidateIndex < 0 || !validIndices.Contains(entry.CandidateIndex))
            {
                throw new CandidateRerankException(
                    CandidateRerankFailureReason.InvalidResponse,
                    $"Azure OpenAI candidate reranking referenced candidate index {entry.CandidateIndex} which is not in the request.");
            }

            if (!seenRankingIndices.Add(entry.CandidateIndex))
            {
                throw new CandidateRerankException(
                    CandidateRerankFailureReason.InvalidResponse,
                    $"Azure OpenAI candidate reranking returned duplicate candidate index {entry.CandidateIndex} in ranking.");
            }

            ranking.Add(new RerankedCandidateScore(entry.CandidateIndex, (double)entry.RelevanceScore));
        }

        int? selectedCandidateIndex = modelResponse.SelectedCandidateIndex;

        if (decision == CandidateRerankDecision.Selected)
        {
            if (selectedCandidateIndex is null)
            {
                throw new CandidateRerankException(
                    CandidateRerankFailureReason.InvalidResponse,
                    "Azure OpenAI candidate reranking returned decision=Selected with a null selectedCandidateIndex.");
            }

            if (selectedCandidateIndex < 0 || !validIndices.Contains(selectedCandidateIndex.Value))
            {
                throw new CandidateRerankException(
                    CandidateRerankFailureReason.InvalidResponse,
                    $"Azure OpenAI candidate reranking selected an index ({selectedCandidateIndex}) that is not in the request.");
            }
        }
        else if (selectedCandidateIndex is not null)
        {
            throw new CandidateRerankException(
                CandidateRerankFailureReason.InvalidResponse,
                $"Azure OpenAI candidate reranking returned decision={decision} with a non-null selectedCandidateIndex.");
        }

        return new CandidateRerankResult(
            decision,
            selectedCandidateIndex,
            Confidence: Math.Clamp((double)modelResponse.Confidence, 0d, 1d),
            Ranking: ranking,
            Reason: modelResponse.Reason);
    }

    private static string BuildUserMessage(CandidateRerankRequest request)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"Task:\n{request.Task}\n");
        builder.AppendLine($"Query:\n{request.Query}\n");

        if (!string.IsNullOrWhiteSpace(request.Context))
        {
            builder.AppendLine($"Context:\n{request.Context}\n");
        }

        builder.AppendLine("Candidates:");

        foreach (var candidate in request.Candidates)
        {
            builder.AppendLine($"[{candidate.Index}] {candidate.DisplayText}");

            if (!string.IsNullOrWhiteSpace(candidate.Metadata))
            {
                builder.AppendLine($"    {candidate.Metadata}");
            }
        }

        return builder.ToString();
    }

    private static CandidateRerankException MapClientResultException(ClientResultException ex)
    {
        var reason = ex.Status switch
        {
            401 or 403 => CandidateRerankFailureReason.Authentication,
            429 => CandidateRerankFailureReason.RateLimited,
            408 => CandidateRerankFailureReason.Timeout,
            >= 500 => CandidateRerankFailureReason.ProviderUnavailable,
            _ => CandidateRerankFailureReason.InvalidResponse
        };

        return new CandidateRerankException(reason, $"Azure OpenAI candidate reranking failed: {ex.Message}");
    }
}
