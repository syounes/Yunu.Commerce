namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// Application port that resolves a free-text category hint (typically
/// produced by the Intent Rewriter) into an official, SQL-Server-validated
/// Google Product Taxonomy category (docs task: "Google Category
/// Resolution"). Never persists anything and never invents an ID: exact
/// match and semantic search results are always confirmed against SQL Server
/// before being returned as Resolved.
/// </summary>
public interface IGoogleCategoryResolver
{
    Task<ResolveGoogleCategoryResult> ResolveAsync(
        ResolveGoogleCategoryRequest request,
        CancellationToken cancellationToken);
}
