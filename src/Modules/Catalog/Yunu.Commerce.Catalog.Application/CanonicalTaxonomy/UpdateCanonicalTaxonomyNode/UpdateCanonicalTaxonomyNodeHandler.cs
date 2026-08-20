using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.UpdateCanonicalTaxonomyNode;

/// <summary>
/// Orchestrates update of a leaf Canonical Taxonomy node not currently used
/// by any Product (docs task: "Canonical Taxonomy + Segments Domain" §22).
/// Only leaf nodes (no children) may have their Name/Description renamed;
/// nodes referenced by a Product's
/// <see cref="Product.CanonicalTaxonomyNodeId"/> cannot be renamed. This
/// conservative rule is preserved unchanged (docs task: "Yunu.Commerce V9 -
/// Canonical Taxonomy Lifecycle + Usage Guards" - rename/update policy):
/// renaming an ancestor would change the Path semantics of its descendants,
/// and no recursive Path recomputation is implemented, so only a leaf
/// without Product usage may be renamed. A Segment association on the node
/// does not additionally block rename: no semantic reason requires it,
/// since renaming does not affect SegmentDefinitionId identity. The rename
/// guard only applies when Name/Description actually change: a pure
/// lifecycle (Status-only) transition on a node with existing Products or
/// children is not a rename and is evaluated by its own Archive usage guard
/// below instead.
///
/// Lifecycle (docs task: "Yunu.Commerce V9 - Canonical Taxonomy Lifecycle +
/// Usage Guards"): Status is optional on the command. When supplied and the
/// requested transition is to Archived, an Archive usage guard runs first:
/// children, Product usage and Approved Segment associations all block
/// Archive. Active &lt;-&gt; Inactive is not usage-guarded (Inactive means
/// "unavailable for new classification, existing references remain valid";
/// see CreateProductHandler, which already requires Active), so a node with
/// existing Products can still transition Active -&gt; Inactive.
/// </summary>
public sealed class UpdateCanonicalTaxonomyNodeHandler
{
    private readonly ICanonicalTaxonomyRepository _canonicalTaxonomyRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICanonicalTaxonomyNodeUsageReader _usageReader;

    public UpdateCanonicalTaxonomyNodeHandler(
        ICanonicalTaxonomyRepository canonicalTaxonomyRepository,
        IProductRepository productRepository,
        ICanonicalTaxonomyNodeUsageReader usageReader)
    {
        _canonicalTaxonomyRepository = canonicalTaxonomyRepository;
        _productRepository = productRepository;
        _usageReader = usageReader;
    }

    public async Task HandleAsync(UpdateCanonicalTaxonomyNodeCommand command, CancellationToken cancellationToken)
    {
        var id = new CanonicalTaxonomyNodeId(command.CanonicalTaxonomyNodeId);
        var node = await _canonicalTaxonomyRepository.GetByIdAsync(id, cancellationToken);

        if (node is null)
        {
            throw new ArgumentException($"Canonical Taxonomy node '{command.CanonicalTaxonomyNodeId}' does not exist.", nameof(command));
        }

        var isRename = !string.Equals(node.Name, command.Name.Trim(), StringComparison.Ordinal)
            || !string.Equals(node.Description, command.Description, StringComparison.Ordinal);

        if (isRename)
        {
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
        }

        var normalizedName = Brand.ComputeNormalizedName(command.Name);
        var newPath = await ComputeNewPathAsync(node, command.Name, cancellationToken);

        node.Update(command.Name, normalizedName, command.Description, newPath);

        if (command.Status is { } statusValue)
        {
            var status = ParseEnum<CanonicalTaxonomyNodeStatus>(statusValue, nameof(command.Status));

            if (status == CanonicalTaxonomyNodeStatus.Archived && node.Status != CanonicalTaxonomyNodeStatus.Archived)
            {
                await EnsureNotInUseForArchiveAsync(id, cancellationToken);
            }

            node.TransitionTo(status);
        }

        await _canonicalTaxonomyRepository.UpdateAsync(node, cancellationToken);
    }

    private async Task EnsureNotInUseForArchiveAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken)
    {
        if (await _canonicalTaxonomyRepository.HasChildrenAsync(id, cancellationToken))
        {
            throw new CanonicalTaxonomyNodeInUseException(
                $"Canonical Taxonomy node '{id.Value}' has at least one child and cannot be archived.");
        }

        if (await _productRepository.ExistsByCanonicalTaxonomyNodeIdAsync(id, cancellationToken))
        {
            throw new CanonicalTaxonomyNodeInUseException(
                $"Canonical Taxonomy node '{id.Value}' is used by at least one Product and cannot be archived.");
        }

        if (await _usageReader.HasApprovedSegmentAssociationAsync(id, cancellationToken))
        {
            throw new CanonicalTaxonomyNodeInUseException(
                $"Canonical Taxonomy node '{id.Value}' has at least one Approved Segment Definition association and cannot be archived.");
        }
    }

    private static TEnum ParseEnum<TEnum>(string value, string paramName) where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
        {
            throw new ArgumentException($"Invalid {paramName}: '{value}'.", paramName);
        }

        return parsed;
    }

    private async Task<string> ComputeNewPathAsync(
        CanonicalTaxonomyNode node,
        string newName,
        CancellationToken cancellationToken)
    {
        if (node.ParentId is null)
        {
            return newName.Trim();
        }

        var parent = await _canonicalTaxonomyRepository.GetByIdAsync(node.ParentId.Value, cancellationToken);

        if (parent is null)
        {
            throw new InvalidOperationException(
                $"Canonical Taxonomy node '{node.Id.Value}' references a parent '{node.ParentId.Value.Value}' that does not exist.");
        }

        return $"{parent.Path.Trim()} > {newName.Trim()}";
    }
}
