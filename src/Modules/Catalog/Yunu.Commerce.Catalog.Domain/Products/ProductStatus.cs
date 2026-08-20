namespace Yunu.Commerce.Catalog.Domain.Products;

/// <summary>
/// Product lifecycle classification (docs task: "Yunu.Commerce V10 - Product
/// + Sku Lifecycle Boundary, Commercial Eligibility e API Governance").
///
/// PendingReview was removed (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md):
/// human review belongs to <see cref="Yunu.Commerce.Catalog.Domain.ProductProposals.ProductProposal"/>
/// (see <see cref="Yunu.Commerce.Catalog.Domain.ProductProposals.ProductProposalStatus"/>),
/// not to the materialized Product. A materialized Product only needs
/// Draft/Active/Inactive/Archived; see <see cref="Product.TransitionTo"/> for
/// the enforced state machine.
/// </summary>
public enum ProductStatus
{
    Draft,
    Active,
    Inactive,
    Archived
}

