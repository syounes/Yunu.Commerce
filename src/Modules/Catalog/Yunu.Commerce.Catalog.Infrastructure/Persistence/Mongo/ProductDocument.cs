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
/// Classification modeling decision: CategoryId is no longer part of the
/// current Product model. [BsonIgnoreExtraElements] preserves compatibility
/// with pre-existing local development documents that still contain a legacy
/// "categoryId" field; that field is silently ignored on read
/// and never written by new code. BrandId is nullable to reflect the optional
/// internal classification. GoogleCategory is required for all newly created
/// Products.
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

    public GoogleCategoryDocument GoogleCategory { get; set; } = null!;

    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Embedded denormalized snapshot of a Product's Google Product Taxonomy
/// classification (docs/domains/catalog.md - external classification systems).
/// </summary>
public sealed class GoogleCategoryDocument
{
    public int Id { get; set; }

    public string Path { get; set; } = string.Empty;
}
