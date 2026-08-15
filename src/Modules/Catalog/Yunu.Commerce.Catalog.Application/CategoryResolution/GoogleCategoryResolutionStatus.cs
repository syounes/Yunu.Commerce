namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// Outcome of resolving a textual category hint into an official Google
/// Product Taxonomy category (docs task: "Google Category Resolution").
/// Mirrors the "Resolved/Ambiguous/NotFound" shape already established for
/// attribute hint resolution (<see
/// cref="Yunu.Commerce.Catalog.Application.AttributeResolution.AttributeResolutionStatus"/>).
/// </summary>
public enum GoogleCategoryResolutionStatus
{
    Resolved,
    Ambiguous,
    NotFound
}
