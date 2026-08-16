namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// How a category/attribute/option resolution reached its final decision
/// (docs task: "Contextual candidate reranking" §15). Shared across Google
/// Category, AttributeDefinition and AttributeOption resolution results so
/// callers can audit whether a result came from a deterministic exact match,
/// the vector algorithm alone, an LLM reranking, or the vector fallback
/// applied after a reranker technical failure.
/// </summary>
public enum ResolutionStrategy
{
    ExactMatch,
    VectorOnly,
    Reranked,
    VectorFallback
}
