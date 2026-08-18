namespace Yunu.Commerce.Catalog.Domain.ProductProposals;

/// <summary>
/// Proposed product-level descriptive data for a <see cref="ProductProposal"/>
/// (docs task: "Catalog intent resolution orchestration" - proposal
/// persistence). <see cref="SuggestedName"/>, <see cref="Description"/> and
/// <see cref="BrandId"/> are intentionally left null: the current resolution
/// pipeline (Intent Rewriter + Google Category Resolution + Attribute Hint
/// Resolution) does not produce them, and this use case must never fabricate
/// values or trigger an additional LLM call to generate them.
/// </summary>
public sealed record ProposedProduct(
    string? SuggestedName,
    string? Description,
    Guid? BrandId,
    ProposedGoogleCategory GoogleCategory);
