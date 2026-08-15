namespace Yunu.Commerce.Api.AI.IntentRewriting;

/// <summary>
/// HTTP request contract for POST /api/ai/intents/rewrite (docs task:
/// "Intent/Query Rewriting"). This endpoint is for initial validation only; it
/// does not connect to retrieval, Product/Sku creation, or persistence.
/// </summary>
public sealed class RewriteIntentRequest
{
    public required string Input { get; init; }

    public string Locale { get; init; } = "pt-BR";
}
