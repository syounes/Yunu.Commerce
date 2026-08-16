namespace Yunu.Commerce.Catalog.Application.ProductProposals;

/// <summary>
/// Input for creating a new ProductProposal from natural-language input
/// (docs task: "Catalog intent resolution orchestration" - proposal
/// persistence). Mirrors <see
/// cref="Yunu.Commerce.Catalog.Application.CatalogIntentResolution.CatalogIntentResolutionRequest"/>,
/// intentionally kept separate so the Application use case does not depend
/// on Host contracts.
/// </summary>
public sealed record CreateProductProposalCommand(string Input, string Locale = "pt-BR");
