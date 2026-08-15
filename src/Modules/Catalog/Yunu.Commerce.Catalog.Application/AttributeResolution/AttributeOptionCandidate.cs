namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// A semantic candidate returned by pgvector search for an Attribute Option
/// and validated against SQL Server (docs task: "Semantic attribute hint
/// resolution" - option observability follow-up). Only includes candidates
/// that were successfully hydrated in Catalog.AttributeOptions: an
/// unvalidated pgvector hit never appears here, since its
/// <see cref="AttributeOptionId"/> would have to be fabricated. Included in
/// the final result regardless of whether the option was ultimately accepted,
/// to help calibrate <c>OptionMinimumSimilarity</c>/<c>MinimumScoreMargin</c>
/// with real data.
/// </summary>
public sealed record AttributeOptionCandidate(
    int AttributeOptionId,
    string OptionCode,
    string OptionName,
    double Similarity);
