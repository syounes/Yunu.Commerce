using Yunu.Commerce.Catalog.Domain.Attributes;

namespace Yunu.Commerce.Catalog.Domain.ProductProposals;

/// <summary>
/// A single proposed attribute assignment for a <see cref="ProposedSku"/>
/// (docs task: "Catalog intent resolution orchestration" - proposal
/// persistence). Reuses the canonical <see cref="AttributeDefinitionId"/>,
/// <see cref="AttributeOptionId"/> and <see cref="SkuAttributeDataType"/>
/// Value Objects/enum already owned by Catalog.Domain instead of duplicating
/// them, since this proposal already carries a SQL-Server-validated
/// AttributeDefinitionId/AttributeOptionId. Technical candidates and
/// rerank/rejection reasons produced during resolution are intentionally not
/// persisted here.
/// </summary>
public sealed record ProposedSkuAttribute(
    AttributeDefinitionId AttributeDefinitionId,
    string AttributeCode,
    string AttributeName,
    int Sequence,
    SkuAttributeDataType DataType,
    string RawName,
    string? RawValue,
    string? NormalizedValue,
    ProposedTypedValue? TypedValue,
    AttributeOptionId? AttributeOptionId,
    string? OptionCode,
    string? OptionName,
    string? DefinitionResolutionStrategy,
    string? OptionResolutionStrategy,
    double? DefinitionSimilarity,
    double? ValueSimilarity,
    double? DefinitionRerankConfidence,
    double? OptionRerankConfidence);
