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
/// </summary>
public sealed class ProductDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid BrandId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid CategoryId { get; set; }

    public string Status { get; set; } = string.Empty;
}
