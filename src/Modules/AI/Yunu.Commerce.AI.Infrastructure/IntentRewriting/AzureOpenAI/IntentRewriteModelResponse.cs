using System.Text.Json.Serialization;

namespace Yunu.Commerce.AI.Infrastructure.IntentRewriting.AzureOpenAI;

/// <summary>
/// Wire-level shape returned by the model under Structured Outputs (docs task:
/// "Intent/Query Rewriting"). Deliberately narrower than <see
/// cref="Yunu.Commerce.AI.Application.IntentRewriting.IntentRewriteResult"/>:
/// <c>originalInput</c> and <c>targetLocale</c> are known from the request and
/// are not asked of the model, avoiding a class of "model echoes back the
/// wrong locale" failures.
/// </summary>
internal sealed class IntentRewriteModelResponse
{
    [JsonPropertyName("normalizedQuery")]
    public string NormalizedQuery { get; init; } = string.Empty;

    [JsonPropertyName("semanticQuery")]
    public string SemanticQuery { get; init; } = string.Empty;

    [JsonPropertyName("intent")]
    public string Intent { get; init; } = "Unknown";

    [JsonPropertyName("detectedLanguage")]
    public string DetectedLanguage { get; init; } = string.Empty;

    [JsonPropertyName("categoryHint")]
    public string? CategoryHint { get; init; }

    [JsonPropertyName("categorySearchQuery")]
    public string? CategorySearchQuery { get; init; }

    [JsonPropertyName("attributeHints")]
    public List<IntentRewriteModelAttributeHint> AttributeHints { get; init; } = [];

    [JsonPropertyName("searchTerms")]
    public List<string> SearchTerms { get; init; } = [];

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; init; }
}

internal sealed class IntentRewriteModelAttributeHint
{
    [JsonPropertyName("rawName")]
    public string RawName { get; init; } = string.Empty;

    [JsonPropertyName("rawValue")]
    public string? RawValue { get; init; }
}
