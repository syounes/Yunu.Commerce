using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.UpdateCanonicalTaxonomyNode;

/// <summary>
/// Orchestrates update of a leaf Canonical Taxonomy node not currently used
/// by any Product (docs task: "Canonical Taxonomy + Segments Domain" §22).
/// Only leaf nodes (no children) may be updated; nodes referenced by a
/// Product's <see cref="Product.CanonicalTaxonomyNodeId"/> are blocked.
/// </summary>
public sealed class UpdateCanonicalTaxonomyNodeHandler
{
    private readonly ICanonicalTaxonomyRepository _canonicalTaxonomyRepository;
    private readonly IProductRepository _productRepository;

    public UpdateCanonicalTaxonomyNodeHandler(
        ICanonicalTaxonomyRepository canonicalTaxonomyRepository,
        IProductRepository productRepository)
    {
        _canonicalTaxonomyRepository = canonicalTaxonomyRepository;
        _productRepository = productRepository;
    }

    public async Task HandleAsync(UpdateCanonicalTaxonomyNodeCommand command, CancellationToken cancellationToken)
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
                $"Canonical Taxonomy node '{command.CanonicalTaxonomyNodeId}' is not a leaf and cannot be updated.");
        }

        if (await _productRepository.ExistsByCanonicalTaxonomyNodeIdAsync(id, cancellationToken))
        {
            throw new CanonicalTaxonomyNodeInUseException(
                $"Canonical Taxonomy node '{command.CanonicalTaxonomyNodeId}' is used by at least one Product and cannot be updated.");
        }

        var normalizedName = Brand.ComputeNormalizedName(command.Name);

        node.Update(command.Name, normalizedName, command.Description);

        await _canonicalTaxonomyRepository.UpdateAsync(node, cancellationToken);
    }
}
