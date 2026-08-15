namespace Yunu.Commerce.Catalog.Application.CatalogIntentResolution;

/// <summary>
/// Application port that interprets natural-language catalog input end to
/// end: Intent Rewriter (once) → Google Category Resolution → Attribute Hint
/// Resolution (docs task: "Catalog intent resolution orchestration"). This is
/// the only entry point Hosts depend on for the "/api/ai/catalog/resolve"
/// use case; it never persists anything and never publishes events.
/// </summary>
public interface ICatalogIntentResolutionOrchestrator
{
    Task<CatalogIntentResolutionResult> ResolveAsync(
        CatalogIntentResolutionRequest request,
        CancellationToken cancellationToken);
}
