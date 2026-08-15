namespace Yunu.Commerce.Catalog.Application.CatalogIntentResolution;

/// <summary>
/// Input to <see cref="ICatalogIntentResolutionOrchestrator"/> (docs task:
/// "Catalog intent resolution orchestration"). <see cref="Input"/> is the raw
/// natural-language text as typed by the user; the orchestrator itself calls
/// the Intent Rewriter exactly once.
/// </summary>
public sealed record CatalogIntentResolutionRequest(string Input, string Locale = "pt-BR");
