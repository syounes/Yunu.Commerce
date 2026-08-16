using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// MongoDB persistence shape for the ProductProposal Aggregate (docs task:
/// "Catalog intent resolution orchestration" - proposal persistence). Exists
/// exclusively in Infrastructure; the ProductProposal Aggregate is never
/// serialized directly and carries no BSON attributes. DomainEvents are
/// intentionally absent: they are never persisted.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class ProductProposalMongoModel
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Locale { get; set; } = string.Empty;

    public ProposalSourceMongoModel Source { get; set; } = null!;

    public ProposedProductMongoModel Product { get; set; } = null!;

    public List<ProposedSkuMongoModel> Skus { get; set; } = new();

    public ProposalResolutionMongoModel Resolution { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? ConfirmedAtUtc { get; set; }

    public DateTime? ConvertedAtUtc { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? CreatedProductId { get; set; }
}

public sealed class ProposalSourceMongoModel
{
    public string OriginalInput { get; set; } = string.Empty;

    public string NormalizedQuery { get; set; } = string.Empty;

    public string SemanticQuery { get; set; } = string.Empty;

    public string Intent { get; set; } = string.Empty;

    public string DetectedLanguage { get; set; } = string.Empty;

    public string TargetLocale { get; set; } = string.Empty;
}

public sealed class ProposedProductMongoModel
{
    public string? SuggestedName { get; set; }

    public string? Description { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? BrandId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? FamilyId { get; set; }

    public ProposedGoogleCategoryMongoModel GoogleCategory { get; set; } = null!;
}

public sealed class ProposedGoogleCategoryMongoModel
{
    public long GoogleCategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public int Depth { get; set; }

    public string? ResolutionStrategy { get; set; }

    public double? Similarity { get; set; }

    public double? RerankConfidence { get; set; }
}

public sealed class ProposedSkuMongoModel
{
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    public string? SuggestedCode { get; set; }

    public string? Gtin { get; set; }

    public List<ProposedSkuAttributeMongoModel> Attributes { get; set; } = new();
}

public sealed class ProposedSkuAttributeMongoModel
{
    public int AttributeDefinitionId { get; set; }

    public string AttributeCode { get; set; } = string.Empty;

    public string AttributeName { get; set; } = string.Empty;

    public int Sequence { get; set; }

    public string DataType { get; set; } = string.Empty;

    public string RawName { get; set; } = string.Empty;

    public string? RawValue { get; set; }

    public string? NormalizedValue { get; set; }

    public ProposedTypedValueMongoModel? TypedValue { get; set; }

    public int? AttributeOptionId { get; set; }

    public string? OptionCode { get; set; }

    public string? OptionName { get; set; }

    public string? DefinitionResolutionStrategy { get; set; }

    public string? OptionResolutionStrategy { get; set; }

    public double? DefinitionSimilarity { get; set; }

    public double? ValueSimilarity { get; set; }

    public double? DefinitionRerankConfidence { get; set; }

    public double? OptionRerankConfidence { get; set; }
}

public sealed class ProposedTypedValueMongoModel
{
    public string DisplayValue { get; set; } = string.Empty;

    public string? TextValue { get; set; }

    public long? IntegerValue { get; set; }

    public decimal? DecimalValue { get; set; }

    public bool? BooleanValue { get; set; }

    public DateTimeOffset? DateTimeValue { get; set; }

    public decimal? MoneyAmount { get; set; }

    public string? CurrencyCode { get; set; }

    public decimal? MeasurementValue { get; set; }

    public string? UnitCode { get; set; }

    public string? JsonValue { get; set; }
}

public sealed class ProposalResolutionMongoModel
{
    public string Status { get; set; } = string.Empty;

    public bool CategoryResolved { get; set; }

    public bool AllAttributesResolved { get; set; }

    public bool ReadyForProposal { get; set; }

    public decimal IntentConfidence { get; set; }

    public List<string> Warnings { get; set; } = new();
}
