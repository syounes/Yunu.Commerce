namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// Outcome of resolving a single category hint (docs task: "Google Category
/// Resolution"). Only <see cref="GoogleCategoryId"/> values that were
/// confirmed active in SQL Server may ever be reported as
/// <see cref="GoogleCategoryResolutionStatus.Resolved"/>; pgvector candidates
/// are never trusted on their own. <see cref="Candidates"/> preserves
/// similarity order and is populated even when <see cref="Status"/> is
/// <see cref="GoogleCategoryResolutionStatus.Ambiguous"/> or
/// <see cref="GoogleCategoryResolutionStatus.NotFound"/>, to help calibration
/// and future user disambiguation.
/// </summary>
public sealed record ResolveGoogleCategoryResult(
    string RawCategoryHint,
    GoogleCategoryResolutionStatus Status,
    long? GoogleCategoryId,
    string? CategoryName,
    string? CategoryPath,
    int? Depth,
    double? Similarity,
    IReadOnlyList<GoogleCategoryCandidate> Candidates,
    string? Reason);
