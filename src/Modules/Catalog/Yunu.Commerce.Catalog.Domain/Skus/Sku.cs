using Yunu.Commerce.Catalog.Domain.Attributes;
using Yunu.Commerce.Catalog.Domain.Attributes.Events;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.Catalog.Domain.Skus.Events;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Skus;

/// <summary>
/// Sku Aggregate Root (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// Sku is an independent Aggregate that references its owning Product only by
/// identity (<see cref="ProductId"/>). Sku has its own lifecycle, persistence
/// boundary and repository contract; it is no longer constructed or owned by
/// the Product Aggregate.
///
/// Sku also owns a collection of validated <see cref="SkuAttribute"/>
/// assignments (docs task: "SKU attribute foundation"). AttributeDefinition
/// and AttributeOption identities are resolved and validated by
/// Catalog.Application against SQL Server before this Aggregate is asked to
/// assign an attribute; Sku only enforces the invariants that do not depend
/// on external reference data (docs task, "Architectural boundaries" §6).
/// </summary>
public sealed class Sku
{
    private readonly List<IDomainEvent> _domainEvents = new();
    private readonly List<SkuAttribute> _attributes = new();
    private readonly List<SegmentAssignment> _segmentAssignments = new();

    public SkuId Id { get; }

    public ProductId ProductId { get; }

    public SkuCode Code { get; }

    public string? Gtin { get; private set; }

    public SkuStatus Status { get; private set; }

    public IReadOnlyCollection<SkuAttribute> Attributes => _attributes;

    public IReadOnlyCollection<SegmentAssignment> SegmentAssignments => _segmentAssignments;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    private Sku(SkuId id, ProductId productId, SkuCode code, string? gtin, SkuStatus status)
    {
        Id = id;
        ProductId = productId;
        Code = code;
        Gtin = gtin;
        Status = status;
    }

    /// <summary>
    /// Creates a new Sku for an existing Product identity. A Sku cannot exist
    /// without a valid <see cref="ProductId"/> (docs/adr/0010 §5).
    /// </summary>
    public static Sku Create(
        SkuId id,
        ProductId productId,
        SkuCode code,
        string? gtin = null,
        SkuStatus status = SkuStatus.Draft)
    {
        var sku = new Sku(id, productId, code, gtin, status);

        sku._domainEvents.Add(new SkuCreatedDomainEvent(id, productId));

        return sku;
    }

    /// <summary>
    /// Rehydrates a Sku, including its previously assigned attributes, from
    /// persisted state without raising creation-time domain events. Used
    /// exclusively by Infrastructure persistence mappers
    /// (docs task: "SKU attribute foundation" - MongoDB persistence).
    /// </summary>
    public static Sku Hydrate(
        SkuId id,
        ProductId productId,
        SkuCode code,
        string? gtin,
        SkuStatus status,
        IEnumerable<SkuAttribute> attributes,
        IEnumerable<SegmentAssignment>? segmentAssignments = null)
    {
        var sku = new Sku(id, productId, code, gtin, status);

        sku._attributes.AddRange(attributes);

        if (segmentAssignments is not null)
        {
            sku._segmentAssignments.AddRange(segmentAssignments);
        }

        return sku;
    }


    /// <summary>
    /// Transitions the Sku to Active. No documented invariant currently restricts
    /// which prior statuses may activate; kept simple until lifecycle rules are
    /// formally defined (docs/domains/catalog.md §20).
    /// </summary>
    public void Activate()
    {
        if (Status == SkuStatus.Active)
        {
            return;
        }

        Status = SkuStatus.Active;
        _domainEvents.Add(new SkuActivatedDomainEvent(Id, ProductId));
    }

    /// <summary>
    /// Transitions the Sku to Inactive ("blocked").
    /// </summary>
    public void Block()
    {
        if (Status == SkuStatus.Inactive)
        {
            return;
        }

        Status = SkuStatus.Inactive;
        _domainEvents.Add(new SkuBlockedDomainEvent(Id, ProductId));
    }

    /// <summary>
    /// Transitions the Sku to Archived ("discontinued").
    /// </summary>
    public void Discontinue()
    {
        if (Status == SkuStatus.Archived)
        {
            return;
        }

        Status = SkuStatus.Archived;
        _domainEvents.Add(new SkuDiscontinuedDomainEvent(Id, ProductId));
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>
    /// Assigns an attribute to this Sku. AttributeDefinitionId and (when
    /// applicable) AttributeOptionId must already be resolved and validated
    /// by Catalog.Application against SQL Server; this method only enforces
    /// invariants that belong to the Sku Aggregate itself.
    ///
    /// A SKU cannot contain two assignments with the same
    /// AttributeDefinitionId and Sequence (docs task: "SKU attribute
    /// foundation" - required invariants). Assigning the same effective value
    /// again is idempotent and raises no event. Assigning a different
    /// effective value for an existing (AttributeDefinitionId, Sequence) pair
    /// must go through <see cref="ReplaceAttribute"/> instead.
    /// </summary>
    public void AssignAttribute(
        AttributeDefinitionId attributeDefinitionId,
        string attributeCode,
        int sequence,
        SkuAttributeValue value,
        AttributeOptionId? attributeOptionId = null,
        SkuAttributeSource source = SkuAttributeSource.User,
        decimal? confidence = null)
    {
        var existing = FindAttribute(attributeDefinitionId, sequence);

        if (existing is not null)
        {
            if (existing.HasSameEffectiveValueAs(value, attributeOptionId))
            {
                return;
            }

            throw new InvalidOperationException(
                $"An attribute assignment already exists for AttributeDefinitionId '{attributeDefinitionId}' and Sequence '{sequence}' with a different value. Use ReplaceAttribute to change it explicitly.");
        }

        var attribute = SkuAttribute.Create(
            attributeDefinitionId,
            attributeCode,
            sequence,
            value,
            attributeOptionId,
            source,
            confidence);

        _attributes.Add(attribute);

        _domainEvents.Add(new SkuAttributeAssignedDomainEvent(Id, attributeDefinitionId, attribute.AttributeCode, sequence));
    }

    /// <summary>
    /// Explicitly replaces the value of an existing attribute assignment
    /// identified by (AttributeDefinitionId, Sequence). Idempotent when the
    /// supplied value is effectively the same as the current one (no event is
    /// raised in that case).
    /// </summary>
    public void ReplaceAttribute(
        AttributeDefinitionId attributeDefinitionId,
        string attributeCode,
        int sequence,
        SkuAttributeValue value,
        AttributeOptionId? attributeOptionId = null,
        SkuAttributeSource source = SkuAttributeSource.User,
        decimal? confidence = null)
    {
        var existing = FindAttribute(attributeDefinitionId, sequence);

        if (existing is null)
        {
            throw new InvalidOperationException(
                $"No attribute assignment exists for AttributeDefinitionId '{attributeDefinitionId}' and Sequence '{sequence}' to replace.");
        }

        if (existing.HasSameEffectiveValueAs(value, attributeOptionId))
        {
            return;
        }

        var replacement = SkuAttribute.Create(
            attributeDefinitionId,
            attributeCode,
            sequence,
            value,
            attributeOptionId,
            source,
            confidence);

        _attributes.Remove(existing);
        _attributes.Add(replacement);

        _domainEvents.Add(new SkuAttributeReplacedDomainEvent(Id, attributeDefinitionId, replacement.AttributeCode, sequence));
    }

    /// <summary>
    /// Removes an existing attribute assignment identified by
    /// (AttributeDefinitionId, Sequence). No-op when the assignment does not
    /// exist.
    /// </summary>
    public void RemoveAttribute(AttributeDefinitionId attributeDefinitionId, int sequence)
    {
        var existing = FindAttribute(attributeDefinitionId, sequence);

        if (existing is null)
        {
            return;
        }

        _attributes.Remove(existing);

        _domainEvents.Add(new SkuAttributeRemovedDomainEvent(Id, attributeDefinitionId, sequence));
    }

    private SkuAttribute? FindAttribute(AttributeDefinitionId attributeDefinitionId, int sequence)
    {
        return _attributes.FirstOrDefault(a => a.AttributeDefinitionId == attributeDefinitionId && a.Sequence == sequence);
    }

    /// <summary>
    /// Assigns a Segment to this Sku. SegmentDefinitionId/SegmentOptionId
    /// identities and reference-data rules (Definition active, AssignmentScope
    /// permits Sku/ProductWithSkuOverride, Option belongs to Definition,
    /// SelectionMode, etc.) must already be resolved and validated by
    /// Catalog.Application against SQL Server before calling this method
    /// (docs task: "Canonical Taxonomy + Segments Domain" §30); this method
    /// only enforces invariants belonging to this Aggregate: no duplicated
    /// SegmentDefinitionId, and idempotent re-assignment of the same effective
    /// options. Only the explicit override is ever persisted here; effective
    /// (inherited) segments are resolved by Application, not by this Aggregate.
    /// </summary>
    public void AssignSegment(SegmentDefinitionId segmentDefinitionId, string segmentCode, IEnumerable<SegmentOptionSelection> options)
    {
        var existing = FindSegmentAssignment(segmentDefinitionId);
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

        _domainEvents.Add(new SkuSegmentAssignedDomainEvent(Id, segmentDefinitionId, assignment.SegmentCode));
    }

    /// <summary>
    /// Explicitly replaces the options of an existing Sku Segment assignment.
    /// Idempotent when the supplied options are effectively the same as the
    /// current ones (no event is raised in that case).
    /// </summary>
    public void ReplaceSegment(SegmentDefinitionId segmentDefinitionId, string segmentCode, IEnumerable<SegmentOptionSelection> options)
    {
        var existing = FindSegmentAssignment(segmentDefinitionId);
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

        _domainEvents.Add(new SkuSegmentReplacedDomainEvent(Id, segmentDefinitionId, replacement.SegmentCode));
    }

    /// <summary>
    /// Removes an existing Sku Segment assignment identified by
    /// SegmentDefinitionId. No-op when the assignment does not exist.
    /// </summary>
    public void RemoveSegment(SegmentDefinitionId segmentDefinitionId)
    {
        var existing = FindSegmentAssignment(segmentDefinitionId);

        if (existing is null)
        {
            return;
        }

        _segmentAssignments.Remove(existing);

        _domainEvents.Add(new SkuSegmentRemovedDomainEvent(Id, segmentDefinitionId));
    }

    private SegmentAssignment? FindSegmentAssignment(SegmentDefinitionId segmentDefinitionId)
    {
        return _segmentAssignments.FirstOrDefault(a => a.SegmentDefinitionId == segmentDefinitionId);
    }
}
