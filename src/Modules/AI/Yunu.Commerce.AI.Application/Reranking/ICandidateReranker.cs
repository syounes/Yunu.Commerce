namespace Yunu.Commerce.AI.Application.Reranking;

/// <summary>
/// Provider-agnostic port for contextual candidate reranking (docs task:
/// "Contextual candidate reranking"). This is the single boundary the
/// Application layer (Catalog resolvers) depends on: it must never reference
/// Azure OpenAI, the OpenAI SDK, a cross-encoder, Cohere or any other vendor
/// directly, mirroring <see
/// cref="Yunu.Commerce.AI.Application.IntentRewriting.IIntentRewriter"/> and
/// <see cref="Yunu.Commerce.AI.Application.Embeddings.IEmbeddingProvider"/>.
/// Completely independent from Google Category, AttributeDefinition or
/// AttributeOption concepts: callers translate their own candidates into
/// <see cref="RerankCandidate"/> and translate the returned index back.
/// </summary>
public interface ICandidateReranker
{
    Task<CandidateRerankResult> RerankAsync(
        CandidateRerankRequest request,
        CancellationToken cancellationToken = default);
}
