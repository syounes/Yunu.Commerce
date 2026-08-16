using System.Text.Json.Serialization;

namespace Yunu.Commerce.AI.Infrastructure.Reranking.AzureOpenAI;

/// <summary>
/// Raw shape deserialized from the Azure OpenAI Structured Output for the
/// Candidate Reranker (docs task: "Contextual candidate reranking"). Mirrors
/// <see cref="CandidateRerankJsonSchema"/> field-for-field. Never trusted
/// directly: <see cref="AzureOpenAICandidateReranker"/> validates every
/// candidate index against the original request before producing a
/// <see cref="Yunu.Commerce.AI.Application.Reranking.CandidateRerankResult"/>.
/// </summary>
internal sealed class CandidateRerankModelResponse
{
    [JsonPropertyName("decision")]
    public required string Decision { get; init; }

    [JsonPropertyName("selectedCandidateIndex")]
    public int? SelectedCandidateIndex { get; init; }

    [JsonPropertyName("confidence")]
    public required decimal Confidence { get; init; }

    [JsonPropertyName("ranking")]
    public required IReadOnlyList<CandidateRerankModelRankingEntry> Ranking { get; init; }

    [JsonPropertyName("reason")]
    public required string Reason { get; init; }
}

internal sealed class CandidateRerankModelRankingEntry
{
    [JsonPropertyName("candidateIndex")]
    public required int CandidateIndex { get; init; }

    [JsonPropertyName("relevanceScore")]
    public required decimal RelevanceScore { get; init; }
}
