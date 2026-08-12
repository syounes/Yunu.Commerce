using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// MongoDB persistence shape for a Sku, embedded inside <see cref="ProductDocument"/>.
/// Exists exclusively in Infrastructure; the Catalog.Domain Sku Entity is never
/// serialized directly (docs/domains/catalog.md §41).
/// </summary>
public sealed class SkuDocument
{
    [BsonRepresentation(BsonType.String)]
    public Guid SkuId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
