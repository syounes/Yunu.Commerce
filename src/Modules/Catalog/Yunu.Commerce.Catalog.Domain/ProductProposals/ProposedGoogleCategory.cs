namespace Yunu.Commerce.Catalog.Domain.ProductProposals;

/// <summary>
/// Denormalized snapshot of the Google Product Taxonomy category resolved
/// for a <see cref="ProductProposal"/> (docs task: "Catalog intent
/// resolution orchestration" - proposal persistence). Only the fields
/// needed for display/audit are kept; technical candidates and rejection
/// reasons produced during resolution are intentionally not persisted here.
/// </summary>
public sealed record ProposedGoogleCategory(
    long GoogleCategoryId,
    string Name,
    string Path,
    int Depth,
    string? ResolutionStrategy,
    double? Similarity,
    double? RerankConfidence);
