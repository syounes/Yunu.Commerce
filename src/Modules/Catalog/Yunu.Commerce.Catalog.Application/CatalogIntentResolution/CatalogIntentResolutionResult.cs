using Yunu.Commerce.AI.Application.IntentRewriting;
using Yunu.Commerce.Catalog.Application.AttributeResolution;
using Yunu.Commerce.Catalog.Application.CategoryResolution;

namespace Yunu.Commerce.Catalog.Application.CatalogIntentResolution;

/// <summary>
/// Consolidated end-to-end outcome produced by
/// <see cref="ICatalogIntentResolutionOrchestrator"/> (docs task: "Catalog
/// intent resolution orchestration"). Preserves every partial result
/// (Intent Rewriter output, category resolution, attribute resolution) so a
/// future chat frontend can present clarification prompts even when the
/// overall status is not <see cref="CatalogIntentResolutionStatus.Resolved"/>.
/// Never persists anything: no Product/Sku is created, no event is
/// published.
/// </summary>
public sealed record CatalogIntentResolutionResult(
    CatalogIntentResolutionStatus Status,
    IntentRewriteResult? Intent,
    ResolveGoogleCategoryResult? Category,
    ResolveAttributeHintsResult? Attributes,
    bool ReadyForProposal,
    IReadOnlyList<string> Warnings);
