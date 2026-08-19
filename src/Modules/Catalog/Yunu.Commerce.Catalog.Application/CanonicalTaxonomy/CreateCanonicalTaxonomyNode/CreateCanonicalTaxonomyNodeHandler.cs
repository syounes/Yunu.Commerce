
using Yunu.Commerce.Catalog.Application.SegmentCatalog;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.CreateCanonicalTaxonomyNode;

/// <summary>
/// Orchestrates creation of a new Canonical Taxonomy node, root or child
/// (docs task: "Canonical Taxonomy + Segments Domain" §19, §21). When a
/// ParentId is supplied, the parent is loaded from SQL Server and Depth/Path
/// are computed from it (docs task §6); the API never supplies Depth/Path
/// as authority. When a SegmentCode is supplied, it is resolved against
/// Catalog.SegmentDefinitions and must be Active.
/// </summary>
public sealed class CreateCanonicalTaxonomyNodeHandler
{
    private readonly ICanonicalTaxonomyRepository _canonicalTaxonomyRepository;
    private readonly ISegmentCatalogRepository _segmentCatalogRepository;

    public CreateCanonicalTaxonomyNodeHandler(
        ICanonicalTaxonomyRepository canonicalTaxonomyRepository,
        ISegmentCatalogRepository segmentCatalogRepository)
    {
        _canonicalTaxonomyRepository = canonicalTaxonomyRepository;
        _segmentCatalogRepository = segmentCatalogRepository;
    }

    public async Task<CreateCanonicalTaxonomyNodeResult> HandleAsync(
        CreateCanonicalTaxonomyNodeCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Code))
        {
            throw new ArgumentException("Code cannot be null, empty or whitespace.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ArgumentException("Name cannot be null, empty or whitespace.", nameof(command));
        }

        var normalizedName = Brand.ComputeNormalizedName(command.Name);

        SegmentDefinitionId? segmentDefinitionId = null;

        if (!string.IsNullOrWhiteSpace(command.SegmentCode))
        {
            var definition = await _segmentCatalogRepository.GetDefinitionByCodeAsync(command.SegmentCode, cancellationToken);

            if (definition is null)
            {
                throw new ArgumentException($"Segment '{command.SegmentCode}' does not exist.", nameof(command));
            }

            if (!string.Equals(definition.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Segment '{command.SegmentCode}' is not active.", nameof(command));
            }

            segmentDefinitionId = new SegmentDefinitionId(definition.SegmentDefinitionId);
        }

        var pendingId = new CanonicalTaxonomyNodeId(0);

        Domain.CanonicalTaxonomy.CanonicalTaxonomyNode node;

        if (command.ParentId is { } parentIdValue)
        {
            var parentId = new CanonicalTaxonomyNodeId(parentIdValue);
            var parent = await _canonicalTaxonomyRepository.GetByIdAsync(parentId, cancellationToken);

            if (parent is null)
            {
                throw new ArgumentException($"Parent node '{parentIdValue}' does not exist.", nameof(command));
            }

            var depth = parent.Depth + 1;
            var path = $"{parent.Path.TrimEnd('/')}/{command.Code.Trim()}";

            node = Domain.CanonicalTaxonomy.CanonicalTaxonomyNode.CreateChild(
                pendingId, parentId, command.Code, command.Name, normalizedName, command.Description,
                depth, path, segmentDefinitionId);
        }
        else
        {
            var path = $"/{command.Code.Trim()}";

            node = Domain.CanonicalTaxonomy.CanonicalTaxonomyNode.CreateRoot(
                pendingId, command.Code, command.Name, normalizedName, command.Description,
                path, segmentDefinitionId);
        }

        var assignedId = await _canonicalTaxonomyRepository.AddAsync(node, cancellationToken);

        return new CreateCanonicalTaxonomyNodeResult
        {
            CanonicalTaxonomyNodeId = assignedId.Value
        };
    }
}
