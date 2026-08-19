using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// MongoDB persistence shape for the Product Aggregate. Exists exclusively in
/// Infrastructure; the Product Aggregate is never serialized directly and carries
/// no BSON attributes (docs/domains/catalog.md §41, docs/adr/0003 §9).
/// DomainEvents are intentionally absent: they are never persisted.
///
/// SKU data is no longer embedded here (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md);
/// Skus are persisted independently in the "skus" collection.
///
/// Classification modeling decision (docs task: "Canonical Taxonomy + Segments
/// Domain" §13): Product's classification is now CanonicalTaxonomyNodeId only.
/// Name/NormalizedName/Path/Depth/Source/GoogleCategoryId belong to the SQL
/// Server reference catalog and are never persisted here; they are enriched
/// only at read time. [BsonIgnoreExtraElements] preserves compatibility with
/// pre-existing local development documents that still contain legacy fields
/// (e.g. "categoryId", "googleCategory"); those fields are silently ignored
/// on read and never written by new code. BrandId is nullable to reflect the
/// optional internal classification.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ProductDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? BrandId { get; set; }

    public long CanonicalTaxonomyNodeId { get; set; }

    public List<SegmentAssignmentDocument> SegmentAssignments { get; set; } = new();

    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Embedded persisted shape of a single Segment assignment
/// (docs task: "Canonical Taxonomy + Segments Domain" §11-§12). Only the
/// resolved identity and stable codes are persisted; Name/NormalizedName/
/// SemanticText belong to the SQL Server reference catalog and are enriched
/// only at read time.
/// </summary>
public sealed class SegmentAssignmentDocument
{
    public long SegmentDefinitionId { get; set; }

    public string SegmentCode { get; set; } = string.Empty;

    public List<SegmentOptionSelectionDocument> Options { get; set; } = new();
}

/// <summary>
/// Embedded persisted shape of a single selected option within a Segment
/// assignment.
/// </summary>
public sealed class SegmentOptionSelectionDocument
{
    public long SegmentOptionId { get; set; }

    public string OptionCode { get; set; } = string.Empty;
}

