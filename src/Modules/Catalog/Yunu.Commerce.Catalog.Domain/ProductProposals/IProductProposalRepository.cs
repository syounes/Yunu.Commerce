namespace Yunu.Commerce.Catalog.Domain.ProductProposals;

/// <summary>
/// Persistence port for the <see cref="ProductProposal"/> Aggregate Root
/// (docs task: "Catalog intent resolution orchestration" - proposal
/// persistence). Mirrors the shape of <see
/// cref="Yunu.Commerce.Catalog.Domain.Products.IProductRepository"/>: only
/// the persistence needs actually required by the current use cases
/// (create, read by id) are exposed.
/// </summary>
public interface IProductProposalRepository
{
    Task AddAsync(
        ProductProposal proposal,
        CancellationToken cancellationToken);

    Task<ProductProposal?> GetByIdAsync(
        ProductProposalId id,
        CancellationToken cancellationToken);
}
