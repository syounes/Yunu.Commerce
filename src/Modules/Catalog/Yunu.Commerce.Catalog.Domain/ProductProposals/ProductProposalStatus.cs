namespace Yunu.Commerce.Catalog.Domain.ProductProposals;

/// <summary>
/// Lifecycle status of a <see cref="ProductProposal"/> (docs task: "Catalog
/// intent resolution orchestration" - proposal persistence). Only
/// <see cref="AwaitingReview"/> is produced by the current use case;
/// Confirmed/Converted/Rejected/Failed are reserved for future use cases
/// (confirmation, conversion to Product/Sku, rejection) and are not yet
/// implemented.
/// </summary>
public enum ProductProposalStatus
{
    AwaitingReview = 1,
    Confirmed = 2,
    Converted = 3,
    Rejected = 4,
    Failed = 5
}
