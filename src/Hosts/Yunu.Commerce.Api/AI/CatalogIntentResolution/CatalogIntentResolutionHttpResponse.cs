using Yunu.Commerce.Api.AI.AttributeResolution;
using Yunu.Commerce.Api.AI.CategoryResolution;
using Yunu.Commerce.Api.AI.IntentRewriting;

namespace Yunu.Commerce.Api.AI.CatalogIntentResolution;

/// <summary>
/// HTTP response contract for POST /api/ai/catalog/resolve (docs task:
/// "Catalog intent resolution orchestration"). Preserves every partial
/// result so a chat frontend can present clarification prompts even when
/// <see cref="ReadyForProposal"/> is false.
/// </summary>
public sealed class CatalogIntentResolutionHttpResponse
{
    public required string Status { get; init; }

    public RewriteIntentResponse? Intent { get; init; }

    public ResolveGoogleCategoryHttpResponse? Category { get; init; }

    public ResolveAttributeHintsHttpResponse? Attributes { get; init; }

    public required bool ReadyForProposal { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
