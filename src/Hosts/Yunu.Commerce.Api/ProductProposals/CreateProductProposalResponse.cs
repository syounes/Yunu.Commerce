namespace Yunu.Commerce.Api.ProductProposals;

/// <summary>
/// HTTP response contract for POST /api/catalog/product-proposals (docs
/// task: "Catalog intent resolution orchestration" - proposal persistence).
/// </summary>
public sealed class CreateProductProposalResponse
{
    public required Guid ProposalId { get; init; }

    public required string Status { get; init; }

    public required bool ReadyForReview { get; init; }

    public required DateTime CreatedAtUtc { get; init; }
}
