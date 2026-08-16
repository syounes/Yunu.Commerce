namespace Yunu.Commerce.Catalog.Application.ProductProposals;

/// <summary>
/// Outcome of <see cref="CreateProductProposalHandler"/> (docs task: "Catalog
/// intent resolution orchestration" - proposal persistence).
/// </summary>
public sealed record CreateProductProposalResult(
    Guid ProposalId,
    string Status,
    bool ReadyForReview,
    DateTime CreatedAtUtc);
