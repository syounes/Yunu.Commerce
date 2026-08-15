namespace Yunu.Commerce.Api.AI.IntentRewriting;

/// <summary>
/// HTTP response contract for POST /api/ai/intents/rewrite (docs task:
/// "Intent/Query Rewriting"). Mirrors <see
/// cref="Yunu.Commerce.AI.Application.IntentRewriting.IntentRewriteResult"/>
/// exactly; kept as a distinct type so the HTTP contract can evolve
/// independently of the Application-layer result.
/// </summary>
public sealed class RewriteIntentResponse
{
    public required string OriginalInput { get; init; }

    public required string NormalizedQuery { get; init; }

    public required string SemanticQuery { get; init; }

    public required string Intent { get; init; }

    public required string DetectedLanguage { get; init; }

    public required string TargetLocale { get; init; }

    public string? CategoryHint { get; init; }

    public IReadOnlyList<AttributeHintResponse> AttributeHints { get; init; } = Array.Empty<AttributeHintResponse>();

    public IReadOnlyList<string> SearchTerms { get; init; } = Array.Empty<string>();

    public required decimal Confidence { get; init; }
}

public sealed class AttributeHintResponse
{
    public required string Name { get; init; }

    public string? Value { get; init; }
}
