using Yunu.Commerce.Catalog.Application.AttributeResolution;

namespace Yunu.Commerce.Catalog.Application.Tests.CatalogIntentResolution;

/// <summary>
/// Test-only fake for IAttributeHintResolver. Returns a preconfigured result
/// and records the last request received so orchestrator tests can assert
/// which GoogleCategoryId was passed through.
/// </summary>
internal sealed class FakeAttributeHintResolver : IAttributeHintResolver
{
    private readonly ResolveAttributeHintsResult _result;

    public FakeAttributeHintResolver(ResolveAttributeHintsResult result)
    {
        _result = result;
    }

    public ResolveAttributeHintsRequest? LastRequest { get; private set; }

    public Task<ResolveAttributeHintsResult> ResolveAsync(ResolveAttributeHintsRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(_result);
    }
}
