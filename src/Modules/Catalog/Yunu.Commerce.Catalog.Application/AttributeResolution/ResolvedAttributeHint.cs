using Yunu.Commerce.Catalog.Application.CategoryResolution;

namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Resolution outcome for a single attribute hint (docs task: "Semantic
/// attribute hint resolution"). <see cref="RawName"/>/<see cref="RawValue"/>
/// are preserved verbatim from the input; every other field is derived from
/// SQL Server (the source of truth) after validating any pgvector candidate.
/// IDs and codes are never fabricated by this type or its producer.
/// </summary>
public sealed record ResolvedAttributeHint(
    string RawName,
    string? RawValue,
    AttributeResolutionStatus Status,
    int? AttributeDefinitionId,
    string? AttributeCode,
    string? AttributeName,
    string? DataType,
    string? NormalizedValue,
    int? AttributeOptionId,
    string? OptionCode,
    string? OptionName,
    double? DefinitionSimilarity,
    double? ValueSimilarity,
    AttributeRequirementLevel? RequirementLevel,
    IReadOnlyList<AttributeCandidate> Candidates,
    string? Reason,
    IReadOnlyList<AttributeOptionCandidate> OptionCandidates)
{
    /// <summary>
    /// Backwards-compatible constructor for call sites that do not (yet)
    /// produce option candidates (e.g. non-Enum attributes or hints that
    /// never reached option resolution).
    /// </summary>
    public ResolvedAttributeHint(
        string rawName,
        string? rawValue,
        AttributeResolutionStatus status,
        int? attributeDefinitionId,
        string? attributeCode,
        string? attributeName,
        string? dataType,
        string? normalizedValue,
        int? attributeOptionId,
        string? optionCode,
        string? optionName,
        double? definitionSimilarity,
        double? valueSimilarity,
        AttributeRequirementLevel? requirementLevel,
        IReadOnlyList<AttributeCandidate> candidates,
        string? reason)
        : this(
            rawName,
            rawValue,
            status,
            attributeDefinitionId,
            attributeCode,
            attributeName,
            dataType,
            normalizedValue,
            attributeOptionId,
            optionCode,
            optionName,
            definitionSimilarity,
            valueSimilarity,
            requirementLevel,
            candidates,
            reason,
            [])
    {
    }

    /// <summary>
    /// How the definition/option was ultimately resolved (docs task:
    /// "Contextual candidate reranking" §15). Null when the hint was never
    /// resolved (e.g. NotFound before any candidate list existed).
    /// </summary>
    public ResolutionStrategy? DefinitionStrategy { get; init; }

    /// <summary>
    /// Reranker confidence for the attribute definition selection, present
    /// only when <see cref="DefinitionStrategy"/> is
    /// <see cref="ResolutionStrategy.Reranked"/>. Distinct from
    /// <see cref="DefinitionSimilarity"/> (vector similarity): the two
    /// metrics are never conflated.
    /// </summary>
    public double? DefinitionRerankConfidence { get; init; }

    public string? DefinitionRerankReason { get; init; }

    /// <summary>
    /// How the attribute option was ultimately resolved, when applicable
    /// (Enum attributes only).
    /// </summary>
    public ResolutionStrategy? OptionStrategy { get; init; }

    /// <summary>
    /// Typed, structured representation of <see cref="NormalizedValue"/>
    /// (docs task: "Semantic attribute hint resolution" - typed attribute
    /// value preservation), populated for resolved non-Enum attributes.
    /// <see cref="NormalizedValue"/> is always equivalent to
    /// <c>TypedValue.DisplayValue</c> when this is not null; preserved
    /// separately for backwards compatibility and display. Enum attributes
    /// never populate this: their official identity remains
    /// AttributeOptionId/OptionCode/OptionName.
    /// </summary>
    public ResolvedAttributeValue? TypedValue { get; init; }

    /// <summary>
    /// Reranker confidence for the attribute option selection, present only
    /// when <see cref="OptionStrategy"/> is
    /// <see cref="ResolutionStrategy.Reranked"/>. Distinct from
    /// <see cref="ValueSimilarity"/> (vector similarity).
    /// </summary>
    public double? OptionRerankConfidence { get; init; }

    public string? OptionRerankReason { get; init; }
}
