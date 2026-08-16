namespace Yunu.Commerce.Api.AI.CategoryResolution;

/// <summary>
/// HTTP request contract for POST /api/ai/categories/resolve (docs task:
/// "Google Category Resolution"). Used for isolated calibration of the
/// Category Resolver, independent from the end-to-end catalog intent
/// resolution endpoint.
/// </summary>
public sealed class ResolveGoogleCategoryHttpRequest
{
    public required string RawCategoryHint { get; init; }

    public string? CategorySearchQuery { get; init; }

    public string? SemanticQuery { get; init; }

    public string Locale { get; init; } = "pt-BR";
}
