using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products.Events;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Products;

/// <summary>
/// Product Aggregate Root (docs/domains/catalog.md §4-§6).
/// Owns the canonical descriptive identity of a commercial product.
///
/// Modeling decision (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md):
/// Sku is now an independent Aggregate Root that references this Product only by
/// <see cref="ProductId"/>. Product no longer owns, constructs or persists Sku
/// state; composition of Product + Skus for read purposes belongs to the
/// Application/read-model layer, not to this Aggregate.
///
/// Classification modeling decision (docs task: "Canonical Taxonomy + Segments
/// Domain" §13): Product's mandatory classification is now
/// <see cref="CanonicalTaxonomyNodeId"/>, resolved and validated by Application
/// against SQL Server (Catalog.CanonicalTaxonomyNodes) before this Aggregate is
/// constructed. GoogleCategoryId is no longer the Product's classification;
/// Google Taxonomy remains available as an external mapping used by other
/// flows, but Product does not depend on it. BrandId remains optional, because
/// internal Yunu classification/mapping may be assigned after creation.
///
/// Segment assignments (docs task §14) are explicit, resolved-and-validated-by-
/// Application selections; Product does not consult SQL Server itself.
///
/// Lifecycle transition behavior (Activate/Deactivate/Archive/SubmitForReview) is
/// intentionally deferred until a documented use case defines the transition rules
/// (docs/domains/catalog.md §21).
/// </summary>
public sealed class Product
{
    private readonly List<IDomainEvent> _domainEvents = new();
    private readonly List<SegmentAssignment> _segmentAssignments = new();

    public ProductId Id { get; }

    public ProductName Name { get; private set; }

    /// <summary>
    /// Optional free-text description of the Product. Kept as a plain string
    /// (not a Value Object) because no validation/business rule currently
    /// justifies one; introduce one later only if a documented rule requires it.
    /// </summary>
    public string? Description { get; private set; }

    public BrandId? BrandId { get; }

    public CanonicalTaxonomyNodeId CanonicalTaxonomyNodeId { get; }

    public ProductStatus Status { get; private set; }

    public IReadOnlyCollection<SegmentAssignment> SegmentAssignments => _segmentAssignments;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    private Product(
        ProductId id,
        ProductName name,
        string? description,
        BrandId? brandId,
        CanonicalTaxonomyNodeId canonicalTaxonomyNodeId,
        ProductStatus status)
    {
        Id = id;
        Name = name;
        Description = description;
        BrandId = brandId;
        CanonicalTaxonomyNodeId = canonicalTaxonomyNodeId;
        Status = status;
    }

    public static Product Create(
        ProductId id,
        ProductName name,
        string? description,
        BrandId? brandId,
        CanonicalTaxonomyNodeId canonicalTaxonomyNodeId,
        ProductStatus status = ProductStatus.Draft)
    {
        var product = new Product(id, name, description, brandId, canonicalTaxonomyNodeId, status);

        product._domainEvents.Add(new ProductCreatedDomainEvent(id));

        return product;
    }

    /// <summary>
    /// Rehydrates a Product, including its previously assigned Segments, from
    /// persisted state without raising creation-time domain events. Used
    /// exclusively by Infrastructure persistence mappers.
    /// </summary>
    public static Product Hydrate(
        ProductId id,
        ProductName name,
        string? description,
        BrandId? brandId,
        CanonicalTaxonomyNodeId canonicalTaxonomyNodeId,
        ProductStatus status,
        IEnumerable<SegmentAssignment> segmentAssignments)
    {
        var product = new Product(id, name, description, brandId, canonicalTaxonomyNodeId, status);

        product._segmentAssignments.AddRange(segmentAssignments);

        return product;
    }

    /// <summary>
    /// Renames the Product. Raises <see cref="ProductRenamedDomainEvent"/> only when
    /// the new name is different from the current name (docs/domains/catalog.md §38).
    /// </summary>
    public void Rename(ProductName newName)
    {
        ArgumentNullException.ThrowIfNull(newName);

        if (Name == newName)
        {
            return;
        }

        var previousName = Name;
        Name = newName;

        _domainEvents.Add(new ProductRenamedDomainEvent(Id, previousName, newName));
    }

    /// <summary>
    /// Assigns a Segment to this Product. SegmentDefinitionId/SegmentOptionId
    /// identities and reference-data rules (Definition active, AssignmentScope
    /// permits Product, Option belongs to Definition, SelectionMode, etc.) must
    /// already be resolved and validated by Catalog.Application against SQL
    /// Server before calling this method (docs task §28); this method only
    /// enforces invariants belonging to this Aggregate: no duplicated
    /// SegmentDefinitionId, and idempotent re-assignment of the same effective
    /// options.
    /// </summary>
    public void AssignSegment(SegmentDefinitionId segmentDefinitionId, string segmentCode, IEnumerable<SegmentOptionSelection> options)
    {
        var existing = FindAssignment(segmentDefinitionId);
        var materializedOptions = options.ToList();

        if (existing is not null)
        {
            if (existing.HasSameEffectiveOptionsAs(materializedOptions))
            {
                return;
            }

            throw new InvalidOperationException(
                $"A Segment assignment already exists for SegmentDefinitionId '{segmentDefinitionId}' with a different value. Use ReplaceSegment to change it explicitly.");
        }

        var assignment = SegmentAssignment.Create(segmentDefinitionId, segmentCode, materializedOptions);

        _segmentAssignments.Add(assignment);

        _domainEvents.Add(new ProductSegmentAssignedDomainEvent(Id, segmentDefinitionId, assignment.SegmentCode));
    }

    /// <summary>
    /// Explicitly replaces the options of an existing Segment assignment.
    /// Idempotent when the supplied options are effectively the same as the
    /// current ones (no event is raised in that case).
    /// </summary>
    public void ReplaceSegment(SegmentDefinitionId segmentDefinitionId, string segmentCode, IEnumerable<SegmentOptionSelection> options)
    {
        var existing = FindAssignment(segmentDefinitionId);
        var materializedOptions = options.ToList();

        if (existing is null)
        {
            throw new InvalidOperationException(
                $"No Segment assignment exists for SegmentDefinitionId '{segmentDefinitionId}' to replace.");
        }

        if (existing.HasSameEffectiveOptionsAs(materializedOptions))
        {
            return;
        }

        var replacement = SegmentAssignment.Create(segmentDefinitionId, segmentCode, materializedOptions);

        _segmentAssignments.Remove(existing);
        _segmentAssignments.Add(replacement);

        _domainEvents.Add(new ProductSegmentReplacedDomainEvent(Id, segmentDefinitionId, replacement.SegmentCode));
    }

    /// <summary>
    /// Removes an existing Segment assignment identified by
    /// SegmentDefinitionId. No-op when the assignment does not exist.
    /// </summary>
    public void RemoveSegment(SegmentDefinitionId segmentDefinitionId)
    {
        var existing = FindAssignment(segmentDefinitionId);

        if (existing is null)
        {
            return;
        }

        _segmentAssignments.Remove(existing);

        _domainEvents.Add(new ProductSegmentRemovedDomainEvent(Id, segmentDefinitionId));
    }

    private SegmentAssignment? FindAssignment(SegmentDefinitionId segmentDefinitionId)
    {
        return _segmentAssignments.FirstOrDefault(a => a.SegmentDefinitionId == segmentDefinitionId);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
