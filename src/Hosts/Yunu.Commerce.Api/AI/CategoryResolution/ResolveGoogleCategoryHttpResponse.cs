namespace Yunu.Commerce.Api.AI.CategoryResolution;

/// <summary>
/// HTTP response DTOs for POST /api/ai/categories/resolve (docs task:
/// "Google Category Resolution").
/// </summary>
public sealed class GoogleCategoryCandidateDto
{
    public required long GoogleCategoryId { get; init; }

    public required string CategoryName { get; init; }

    public required string CategoryPath { get; init; }

    public int? Depth { get; init; }

    public required double Similarity { get; init; }
}

public sealed class ResolveGoogleCategoryHttpResponse
{
    public required string RawCategoryHint { get; init; }

    public required string Status { get; init; }

    public long? GoogleCategoryId { get; init; }

    public string? CategoryName { get; init; }

    public string? CategoryPath { get; init; }

    public int? Depth { get; init; }

    public double? Similarity { get; init; }

    public required IReadOnlyList<GoogleCategoryCandidateDto> Candidates { get; init; }

    public string? Reason { get; init; }

    public string? ResolutionStrategy { get; init; }

    public double? RerankConfidence { get; init; }

    public string? RerankReason { get; init; }
}
