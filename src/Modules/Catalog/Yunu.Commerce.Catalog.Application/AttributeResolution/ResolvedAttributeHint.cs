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
}
