namespace Yunu.Commerce.Api.AI.AttributeResolution;

/// <summary>
/// HTTP response for POST /api/ai/attributes/resolve (docs task: "Semantic
/// attribute hint resolution"). Mirrors
/// <see cref="Yunu.Commerce.Catalog.Application.AttributeResolution.ResolveAttributeHintsResult"/>.
/// </summary>
public sealed class ResolveAttributeHintsHttpResponse
{
    public required IReadOnlyList<ResolvedAttributeHintDto> Attributes { get; init; }

    public required bool AllResolved { get; init; }
}

public sealed class ResolvedAttributeHintDto
{
    public required string RawName { get; init; }

    public string? RawValue { get; init; }

    public required string Status { get; init; }

    public int? AttributeDefinitionId { get; init; }

    public string? AttributeCode { get; init; }

    public string? AttributeName { get; init; }

    public string? DataType { get; init; }

    public string? NormalizedValue { get; init; }

    public ResolvedAttributeValueDto? TypedValue { get; init; }

    public int? AttributeOptionId { get; init; }

    public string? OptionCode { get; init; }

    public string? OptionName { get; init; }

    public double? DefinitionSimilarity { get; init; }

    public double? ValueSimilarity { get; init; }

    public string? RequirementLevel { get; init; }

    public IReadOnlyList<AttributeCandidateDto> Candidates { get; init; } = Array.Empty<AttributeCandidateDto>();

    public IReadOnlyList<AttributeOptionCandidateDto> OptionCandidates { get; init; } = Array.Empty<AttributeOptionCandidateDto>();

    public string? Reason { get; init; }

    public string? DefinitionResolutionStrategy { get; init; }

    public double? DefinitionRerankConfidence { get; init; }

    public string? DefinitionRerankReason { get; init; }

    public string? OptionResolutionStrategy { get; init; }

    public double? OptionRerankConfidence { get; init; }

    public string? OptionRerankReason { get; init; }
}

public sealed class AttributeCandidateDto
{
    public required int AttributeDefinitionId { get; init; }

    public required string AttributeCode { get; init; }

    public required string AttributeName { get; init; }

    public required double Similarity { get; init; }
}

public sealed class AttributeOptionCandidateDto
{
    public required int AttributeOptionId { get; init; }

    public required string OptionCode { get; init; }

    public required string OptionName { get; init; }

    public required double Similarity { get; init; }
}

/// <summary>
/// Wire contract mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.AttributeResolution.ResolvedAttributeValue"/>.
/// Only the field(s) matching the attribute's DataType are populated.
/// </summary>
public sealed class ResolvedAttributeValueDto
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
