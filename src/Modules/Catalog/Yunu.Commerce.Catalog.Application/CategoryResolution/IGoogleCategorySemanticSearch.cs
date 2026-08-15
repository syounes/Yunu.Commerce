namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// A pgvector semantic search hit for a Google Taxonomy category
/// (public.google_taxonomy_embeddings). <see cref="GoogleCategoryId"/> and
/// <see cref="CategoryPath"/> come directly from the embeddings table for
/// convenience/logging, but must still be hydrated and validated against SQL
/// Server (GoogleTaxonomyCategories, the source of truth) before being
/// trusted as Resolved.
/// </summary>
public sealed record GoogleCategorySemanticCandidate(
    long GoogleCategoryId,
    string CategoryPath,
    double Similarity);

/// <summary>
/// Read-only port for pgvector semantic search over Google Taxonomy category
/// embeddings (public.google_taxonomy_embeddings) (docs task: "Google
/// Category Resolution"). Never validates existence or activity: that
/// responsibility belongs to <see cref="IGoogleCategoryCatalogReader"/> (SQL
/// Server, the source of truth).
/// </summary>
public interface IGoogleCategorySemanticSearch
{
    Task<IReadOnlyList<GoogleCategorySemanticCandidate>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        string locale,
        CancellationToken cancellationToken);
}
