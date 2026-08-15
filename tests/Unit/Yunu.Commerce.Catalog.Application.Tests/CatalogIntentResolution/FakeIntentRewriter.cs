using Yunu.Commerce.AI.Application.IntentRewriting;

namespace Yunu.Commerce.Catalog.Application.Tests.CatalogIntentResolution;

/// <summary>
/// Test-only fake for IIntentRewriter. Returns a preconfigured result and
/// tracks call count so orchestrator tests can assert the Intent Rewriter is
/// called exactly once.
/// </summary>
internal sealed class FakeIntentRewriter : IIntentRewriter
{
    private readonly IntentRewriteResult _result;

    public FakeIntentRewriter(IntentRewriteResult result)
    {
        _result = result;
    }

    public int CallCount { get; private set; }

    public Task<IntentRewriteResult> RewriteAsync(IntentRewriteRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(_result);
    }
}
