namespace Yunu.Commerce.AI.Application.IntentRewriting;

/// <summary>
/// Provider-agnostic port for natural-language catalog intent/query rewriting
/// (docs task: "Intent/Query Rewriting"). This is the single boundary the
/// Application layer depends on: it must never reference Azure OpenAI, the
/// OpenAI SDK, endpoints or credentials directly. Infrastructure supplies one
/// adapter per vendor, mirroring <see
/// cref="Yunu.Commerce.AI.Application.Embeddings.IEmbeddingProvider"/>.
/// </summary>
public interface IIntentRewriter
{
    Task<IntentRewriteResult> RewriteAsync(IntentRewriteRequest request, CancellationToken cancellationToken = default);
}
