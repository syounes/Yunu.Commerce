
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.CreateCanonicalTaxonomyNode;

/// <summary>
/// Orchestrates creation of a new Canonical Taxonomy node, root or child
/// (docs task: "Canonical Taxonomy + Segments Domain" §19, §21). When a
/// ParentId is supplied, the parent is loaded from SQL Server and Depth/Path
/// are computed from it (docs task §6): Path concatenates pt-BR node Names
/// with " > ", never Code and never "/". The API never supplies Depth/Path
/// as authority. Association between a node and Segment Definitions is
/// handled separately through Catalog.CanonicalTaxonomyNodeSegmentDefinitions
/// and is out of scope for this handler.
///
/// Parent lifecycle policy (docs task: "Yunu.Commerce V9 - Canonical
/// Taxonomy Lifecycle + Usage Guards"): a new child cannot be created under
/// an Archived parent, since Archived represents definitive structural
/// retirement. Draft/Active/Inactive parents are all allowed to receive new
/// children: the project does not define a stricter policy for Draft/
/// Inactive, and blocking only Archived is the smallest rule that prevents
/// an evident inconsistency (mirrors
/// Yunu.Commerce.Catalog.Application.SegmentOptions.CreateSegmentOption.CreateSegmentOptionHandler).
/// </summary>
public sealed class CreateCanonicalTaxonomyNodeHandler
{
    private readonly ICanonicalTaxonomyRepository _canonicalTaxonomyRepository;

    public CreateCanonicalTaxonomyNodeHandler(ICanonicalTaxonomyRepository canonicalTaxonomyRepository)
    {
        _canonicalTaxonomyRepository = canonicalTaxonomyRepository;
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

            if (parent.Status == Domain.CanonicalTaxonomy.CanonicalTaxonomyNodeStatus.Archived)
            {
                throw new CanonicalTaxonomyNodeParentArchivedException(
                    $"Parent Canonical Taxonomy node '{parentIdValue}' is Archived and cannot receive new children.");
            }

            var depth = parent.Depth + 1;
            var path = $"{parent.Path.Trim()} > {command.Name.Trim()}";

            node = Domain.CanonicalTaxonomy.CanonicalTaxonomyNode.CreateChild(
                pendingId, parentId, command.Code, command.Name, normalizedName, command.Description,
                depth, path);
        }
        else
        {
            var path = command.Name.Trim();

            node = Domain.CanonicalTaxonomy.CanonicalTaxonomyNode.CreateRoot(
                pendingId, command.Code, command.Name, normalizedName, command.Description,
                path);
        }

        var assignedId = await _canonicalTaxonomyRepository.AddAsync(node, cancellationToken);

        return new CreateCanonicalTaxonomyNodeResult
        {
            CanonicalTaxonomyNodeId = assignedId.Value
        };
    }
}
