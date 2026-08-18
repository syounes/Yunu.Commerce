namespace Yunu.Commerce.Catalog.Application.ProductProposals;

/// <summary>
/// Clean read model for a ProductProposal (docs task: "Catalog intent
/// resolution orchestration" - proposal persistence). Deliberately excludes
/// RAG technical candidates (Candidates/OptionCandidates/RerankReason):
/// only the fields already persisted on the Aggregate are exposed.
/// </summary>
public sealed record GetProductProposalByIdResult(
    Guid ProposalId,
    string Status,
    string Locale,
    ProposalSourceDto Source,
    ProposedProductDto Product,
    IReadOnlyCollection<ProposedSkuDto> Skus,
    ProposalResolutionDto Resolution,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ConfirmedAtUtc,
    DateTime? ConvertedAtUtc,
    Guid? CreatedProductId);

public sealed record ProposalSourceDto(
    string OriginalInput,
    string NormalizedQuery,
    string SemanticQuery,
    string Intent,
    string DetectedLanguage,
    string TargetLocale);

public sealed record ProposedProductDto(
    string? SuggestedName,
    string? Description,
    Guid? BrandId,
    ProposedGoogleCategoryDto GoogleCategory);

public sealed record ProposedGoogleCategoryDto(
    long GoogleCategoryId,
    string Name,
    string Path,
    int Depth,
    string? ResolutionStrategy,
    double? Similarity,
    double? RerankConfidence);

public sealed record ProposedSkuDto(
    Guid Id,
    string? SuggestedCode,
    string? Gtin,
    IReadOnlyCollection<ProposedSkuAttributeDto> Attributes);

public sealed record ProposedSkuAttributeDto(
    int AttributeDefinitionId,
    string AttributeCode,
    string AttributeName,
    int Sequence,
    string DataType,
    string RawName,
    string? RawValue,
    string? NormalizedValue,
    ProposedTypedValueDto? TypedValue,
    int? AttributeOptionId,
    string? OptionCode,
    string? OptionName,
    string? DefinitionResolutionStrategy,
    string? OptionResolutionStrategy,
    double? DefinitionSimilarity,
    double? ValueSimilarity,
    double? DefinitionRerankConfidence,
    double? OptionRerankConfidence);

public sealed record ProposedTypedValueDto(
    string DisplayValue,
    string? TextValue,
    long? IntegerValue,
    decimal? DecimalValue,
    bool? BooleanValue,
    DateTimeOffset? DateTimeValue,
    decimal? MoneyAmount,
    string? CurrencyCode,
    decimal? MeasurementValue,
    string? UnitCode,
    string? JsonValue);

public sealed record ProposalResolutionDto(
    string Status,
    bool CategoryResolved,
    bool AllAttributesResolved,
    bool ReadyForProposal,
    decimal IntentConfidence,
    IReadOnlyCollection<string> Warnings);
