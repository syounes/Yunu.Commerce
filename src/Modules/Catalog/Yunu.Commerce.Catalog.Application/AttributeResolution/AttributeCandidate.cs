namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// A semantic candidate returned by pgvector search and considered (but not
/// yet necessarily accepted) for a given hint (docs task: "Semantic attribute
/// hint resolution"). Only included in the final result when
/// <c>IncludeCandidatesInResponse</c> is enabled, to help calibrate
/// thresholds with real data.
/// </summary>
public sealed record AttributeCandidate(
    int AttributeDefinitionId,
    string AttributeCode,
    string AttributeName,
    double Similarity);
