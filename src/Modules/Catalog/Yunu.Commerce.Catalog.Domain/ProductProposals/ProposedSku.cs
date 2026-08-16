namespace Yunu.Commerce.Catalog.Domain.ProductProposals;

/// <summary>
/// A single proposed SKU inside a <see cref="ProductProposal"/> (docs task:
/// "Catalog intent resolution orchestration" - proposal persistence).
/// <see cref="Id"/> only identifies this SKU proposal within the aggregate;
/// it is not yet a canonical <c>SkuId</c>. <see cref="SuggestedCode"/> and
/// <see cref="Gtin"/> remain null because the current resolution pipeline
/// does not produce them.
/// </summary>
public sealed record ProposedSku(
    Guid Id,
    string? SuggestedCode,
    string? Gtin,
    IReadOnlyCollection<ProposedSkuAttribute> Attributes);
