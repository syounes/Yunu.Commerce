using Yunu.Commerce.Catalog.Application.CatalogIntentResolution;

namespace Yunu.Commerce.Catalog.Application.Tests.ProductProposals;

/// <summary>
/// Test-only fake for ICatalogIntentResolutionOrchestrator. Returns a
/// preconfigured result and tracks call count so
/// CreateProductProposalHandler tests can assert the orchestrator is called
/// exactly once and never makes an additional HTTP/LLM call itself.
/// </summary>
internal sealed class FakeCatalogIntentResolutionOrchestrator : ICatalogIntentResolutionOrchestrator
{
    private readonly CatalogIntentResolutionResult _result;
    private readonly CancellationToken? _expectedCancellationToken;

    public FakeCatalogIntentResolutionOrchestrator(CatalogIntentResolutionResult result, CancellationToken? expectedCancellationToken = null)
    {
        _result = result;
        _expectedCancellationToken = expectedCancellationToken;
    }

    public int CallCount { get; private set; }

    public CancellationToken? ReceivedCancellationToken { get; private set; }

    public Task<CatalogIntentResolutionResult> ResolveAsync(
        CatalogIntentResolutionRequest request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        ReceivedCancellationToken = cancellationToken;

        if (_expectedCancellationToken is { } expected && expected != cancellationToken)
        {
            throw new InvalidOperationException("Unexpected CancellationToken received.");
        }

        return Task.FromResult(_result);
    }
}
