namespace Yunu.Commerce.Api.ProductProposals;

/// <summary>
/// HTTP response contract for GET /api/catalog/product-proposals/{id} (docs
/// task: "Catalog intent resolution orchestration" - proposal persistence).
/// Mirrors <see
/// cref="Yunu.Commerce.Catalog.Application.ProductProposals.GetProductProposalByIdResult"/>.
/// Deliberately excludes RAG technical candidates.
/// </summary>
public sealed class GetProductProposalResponse
{
    public required Guid ProposalId { get; init; }

    public required string Status { get; init; }

    public required string Locale { get; init; }

    public required ProposalSourceResponse Source { get; init; }

    public required ProposedProductResponse Product { get; init; }

    public IReadOnlyCollection<ProposedSkuResponse> Skus { get; init; } = Array.Empty<ProposedSkuResponse>();

    public required ProposalResolutionResponse Resolution { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }

    public DateTime? ConfirmedAtUtc { get; init; }

    public DateTime? ConvertedAtUtc { get; init; }

    public Guid? CreatedProductId { get; init; }
}

public sealed class ProposalSourceResponse
{
    public required string OriginalInput { get; init; }

    public required string NormalizedQuery { get; init; }

    public required string SemanticQuery { get; init; }

    public required string Intent { get; init; }

    public required string DetectedLanguage { get; init; }

    public required string TargetLocale { get; init; }
}

public sealed class ProposedProductResponse
{
    public string? SuggestedName { get; init; }

    public string? Description { get; init; }

    public Guid? BrandId { get; init; }

    public Guid? FamilyId { get; init; }

    public required ProposedGoogleCategoryResponse GoogleCategory { get; init; }
}

public sealed class ProposedGoogleCategoryResponse
{
    public required long GoogleCategoryId { get; init; }

    public required string Name { get; init; }

    public required string Path { get; init; }

    public required int Depth { get; init; }

    public string? ResolutionStrategy { get; init; }

    public double? Similarity { get; init; }

    public double? RerankConfidence { get; init; }
}

public sealed class ProposedSkuResponse
{
    public required Guid Id { get; init; }

    public string? SuggestedCode { get; init; }

    public string? Gtin { get; init; }

    public IReadOnlyCollection<ProposedSkuAttributeResponse> Attributes { get; init; } = Array.Empty<ProposedSkuAttributeResponse>();
}

public sealed class ProposedSkuAttributeResponse
{
    public required int AttributeDefinitionId { get; init; }

    public required string AttributeCode { get; init; }

    public required string AttributeName { get; init; }

    public required int Sequence { get; init; }

    public required string DataType { get; init; }

    public required string RawName { get; init; }

    public string? RawValue { get; init; }

    public string? NormalizedValue { get; init; }

    public ProposedTypedValueResponse? TypedValue { get; init; }

    public int? AttributeOptionId { get; init; }

    public string? OptionCode { get; init; }

    public string? OptionName { get; init; }

    public string? DefinitionResolutionStrategy { get; init; }

    public string? OptionResolutionStrategy { get; init; }

    public double? DefinitionSimilarity { get; init; }

    public double? ValueSimilarity { get; init; }

    public double? DefinitionRerankConfidence { get; init; }

    public double? OptionRerankConfidence { get; init; }
}

public sealed class ProposedTypedValueResponse
{
    public required string DisplayValue { get; init; }

    public string? TextValue { get; init; }

    public long? IntegerValue { get; init; }

    public decimal? DecimalValue { get; init; }

    public bool? BooleanValue { get; init; }

    public DateTimeOffset? DateTimeValue { get; init; }

    public decimal? MoneyAmount { get; init; }

    public string? CurrencyCode { get; init; }

    public decimal? MeasurementValue { get; init; }

    public string? UnitCode { get; init; }

    public string? JsonValue { get; init; }
}

public sealed class ProposalResolutionResponse
{
    public required string Status { get; init; }

    public required bool CategoryResolved { get; init; }

    public required bool AllAttributesResolved { get; init; }

    public required bool ReadyForProposal { get; init; }

    public required decimal IntentConfidence { get; init; }

    public IReadOnlyCollection<string> Warnings { get; init; } = Array.Empty<string>();
}
