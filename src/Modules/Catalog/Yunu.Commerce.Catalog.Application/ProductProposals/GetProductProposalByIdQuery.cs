namespace Yunu.Commerce.Catalog.Application.ProductProposals;

/// <summary>
/// Input for retrieving a ProductProposal by identity (docs task: "Catalog
/// intent resolution orchestration" - proposal persistence).
/// </summary>
public sealed record GetProductProposalByIdQuery(Guid ProposalId);
