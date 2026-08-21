
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
///
/// Concurrency (docs task: "Yunu.Commerce - Canonical Taxonomy Concurrency
/// Guard" §7): child creation participates in the parent's own Revision.
/// The parent's Status/Revision is re-checked and its Revision incremented
/// atomically together with the child insert (see
/// <see cref="ICanonicalTaxonomyRepository.AddChildAsync"/>), so a
/// concurrent Archive of the parent and this call cannot both commit: the
/// loser fails explicitly instead of silently producing an Archived parent
/// with a newly-created child. This handler does not retry automatically.
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

        if (command.ParentId is { } parentIdValue)
        {
            var parentId = new CanonicalTaxonomyNodeId(parentIdValue);
            var loaded = await _canonicalTaxonomyRepository.GetWithRevisionAsync(parentId, cancellationToken);

            if (loaded is not { } loadedParent)
            {
                throw new ArgumentException($"Parent node '{parentIdValue}' does not exist.", nameof(command));
            }

            var (parent, parentRevision) = loadedParent;

            if (parent.Status == Domain.CanonicalTaxonomy.CanonicalTaxonomyNodeStatus.Archived)
            {
                throw new CanonicalTaxonomyNodeParentArchivedException(
                    $"Parent Canonical Taxonomy node '{parentIdValue}' is Archived and cannot receive new children.");
            }

            var depth = parent.Depth + 1;
            var path = $"{parent.Path.Trim()} > {command.Name.Trim()}";

            var childNode = Domain.CanonicalTaxonomy.CanonicalTaxonomyNode.CreateChild(
                pendingId, parentId, command.Code, command.Name, normalizedName, command.Description,
                depth, path);

            var result = await _canonicalTaxonomyRepository.AddChildAsync(childNode, parentRevision, cancellationToken);

            switch (result.Outcome)
            {
                case AddCanonicalTaxonomyChildOutcome.Created:
                    return new CreateCanonicalTaxonomyNodeResult
                    {
                        CanonicalTaxonomyNodeId = result.AssignedId!.Value.Value
                    };
                case AddCanonicalTaxonomyChildOutcome.ParentNotFound:
                    throw new ArgumentException($"Parent node '{parentIdValue}' does not exist.", nameof(command));
                case AddCanonicalTaxonomyChildOutcome.ParentArchived:
                    throw new CanonicalTaxonomyNodeParentArchivedException(
                        $"Parent Canonical Taxonomy node '{parentIdValue}' is Archived and cannot receive new children.");
                case AddCanonicalTaxonomyChildOutcome.ParentConcurrencyConflict:
                    throw new CanonicalTaxonomyNodeConcurrencyConflictException(
                        $"Parent Canonical Taxonomy node '{parentIdValue}' was concurrently modified by another writer. Reload the current state and retry explicitly.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unsupported AddChild outcome.");
            }
        }
        else
        {
            var path = command.Name.Trim();

            var rootNode = Domain.CanonicalTaxonomy.CanonicalTaxonomyNode.CreateRoot(
                pendingId, command.Code, command.Name, normalizedName, command.Description,
                path);

            var assignedId = await _canonicalTaxonomyRepository.AddAsync(rootNode, cancellationToken);

            return new CreateCanonicalTaxonomyNodeResult
            {
                CanonicalTaxonomyNodeId = assignedId.Value
            };
        }
    }
}
