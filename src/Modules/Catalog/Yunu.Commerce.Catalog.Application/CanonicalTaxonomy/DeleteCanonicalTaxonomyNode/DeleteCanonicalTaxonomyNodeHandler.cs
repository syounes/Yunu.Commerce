using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.DeleteCanonicalTaxonomyNode;

/// <summary>
/// Orchestrates deletion of a leaf Canonical Taxonomy node not currently used
/// by any Product (docs task: "Canonical Taxonomy + Segments Domain" §22).
/// </summary>
public sealed class DeleteCanonicalTaxonomyNodeHandler
{
    private readonly ICanonicalTaxonomyRepository _canonicalTaxonomyRepository;
    private readonly IProductRepository _productRepository;

    public DeleteCanonicalTaxonomyNodeHandler(
        ICanonicalTaxonomyRepository canonicalTaxonomyRepository,
        IProductRepository productRepository)
    {
        _canonicalTaxonomyRepository = canonicalTaxonomyRepository;
        _productRepository = productRepository;
    }

    public async Task HandleAsync(DeleteCanonicalTaxonomyNodeCommand command, CancellationToken cancellationToken)
    {
        var id = new CanonicalTaxonomyNodeId(command.CanonicalTaxonomyNodeId);
        var node = await _canonicalTaxonomyRepository.GetByIdAsync(id, cancellationToken);

        if (node is null)
        {
            throw new ArgumentException($"Canonical Taxonomy node '{command.CanonicalTaxonomyNodeId}' does not exist.", nameof(command));
        }

        if (await _canonicalTaxonomyRepository.HasChildrenAsync(id, cancellationToken))
        {
            throw new CanonicalTaxonomyNodeNotLeafException(
                $"Canonical Taxonomy node '{command.CanonicalTaxonomyNodeId}' is not a leaf and cannot be deleted.");
        }

        if (await _productRepository.ExistsByCanonicalTaxonomyNodeIdAsync(id, cancellationToken))
        {
            throw new CanonicalTaxonomyNodeInUseException(
                $"Canonical Taxonomy node '{command.CanonicalTaxonomyNodeId}' is used by at least one Product and cannot be deleted.");
        }

        await _canonicalTaxonomyRepository.DeleteAsync(id, cancellationToken);
    }
}
