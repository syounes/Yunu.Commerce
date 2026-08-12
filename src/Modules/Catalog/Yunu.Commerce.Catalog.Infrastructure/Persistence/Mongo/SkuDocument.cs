using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// MongoDB persistence shape for the Sku Aggregate Root, stored in its own
/// "skus" collection (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// Exists exclusively in Infrastructure; the Catalog.Domain Sku Aggregate is never
/// serialized directly. DomainEvents are intentionally absent: they are never persisted.
/// </summary>
public sealed class SkuDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid ProductId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? Gtin { get; set; }

    public string Status { get; set; } = string.Empty;
}
