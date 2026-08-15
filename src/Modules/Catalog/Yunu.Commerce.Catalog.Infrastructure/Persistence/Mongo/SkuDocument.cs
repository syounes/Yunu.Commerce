using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// MongoDB persistence shape for the Sku Aggregate Root, stored in its own
/// "skus" collection (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// Exists exclusively in Infrastructure; the Catalog.Domain Sku Aggregate is never
/// serialized directly. DomainEvents are intentionally absent: they are never persisted.
///
/// Attributes (docs task: "SKU attribute foundation") are persisted as an
/// embedded sub-document array so the complete Sku Aggregate, including its
/// assigned attributes, is written/read atomically with the rest of the
/// document. Legacy documents without an "Attributes" field hydrate as an
/// empty collection (no destructive migration required).
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

    public List<SkuAttributeDocument>? Attributes { get; set; }
}

/// <summary>
/// Embedded MongoDB shape for one Sku attribute assignment
/// (docs task: "SKU attribute foundation"). Mirrors the fields required to
/// hydrate <see cref="Yunu.Commerce.Catalog.Domain.Attributes.SkuAttribute"/>
/// without querying SQL Server again.
///
/// Typed value properties are marked <see cref="BsonIgnoreIfNullAttribute"/>
/// so only the field matching the attribute's <see cref="DataType"/> is
/// written to BSON (docs task: "omit null typed attribute properties from
/// persisted documents"). Missing/absent fields on read still hydrate as
/// null, preserving backward compatibility with existing documents that
/// stored explicit nulls.
/// </summary>
public sealed class SkuAttributeDocument
{
    public int AttributeDefinitionId { get; set; }

    public string AttributeCode { get; set; } = string.Empty;

    public int Sequence { get; set; }

    public string DataType { get; set; } = string.Empty;

    public string? RawValue { get; set; }

    public string NormalizedValue { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? Text { get; set; }

    [BsonIgnoreIfNull]
    public long? Integer { get; set; }

    [BsonIgnoreIfNull]
    public decimal? Decimal { get; set; }

    [BsonIgnoreIfNull]
    public bool? Boolean { get; set; }

    [BsonIgnoreIfNull]
    public DateTime? DateTimeValue { get; set; }

    [BsonIgnoreIfNull]
    public decimal? MoneyAmount { get; set; }

    [BsonIgnoreIfNull]
    public string? CurrencyCode { get; set; }

    [BsonIgnoreIfNull]
    public decimal? MeasurementValue { get; set; }

    [BsonIgnoreIfNull]
    public string? UnitCode { get; set; }

    [BsonIgnoreIfNull]
    public string? Url { get; set; }

    [BsonIgnoreIfNull]
    public string? EnumOptionCode { get; set; }

    [BsonIgnoreIfNull]
    public string? Json { get; set; }

    [BsonIgnoreIfNull]
    public int? AttributeOptionId { get; set; }

    public string Source { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public decimal? Confidence { get; set; }
}

