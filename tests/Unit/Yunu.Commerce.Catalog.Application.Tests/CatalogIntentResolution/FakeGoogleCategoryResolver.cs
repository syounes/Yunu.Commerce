using Yunu.Commerce.Catalog.Application.CategoryResolution;

namespace Yunu.Commerce.Catalog.Application.Tests.CatalogIntentResolution;

/// <summary>
/// Test-only fake for IGoogleCategoryResolver. Returns a preconfigured result
/// and records the last request received so orchestrator tests can assert
/// which categoryHint/semanticQuery were passed through.
/// </summary>
internal sealed class FakeGoogleCategoryResolver : IGoogleCategoryResolver
{
    private readonly ResolveGoogleCategoryResult _result;

    public FakeGoogleCategoryResolver(ResolveGoogleCategoryResult result)
    {
        _result = result;
    }

    public ResolveGoogleCategoryRequest? LastRequest { get; private set; }

    public Task<ResolveGoogleCategoryResult> ResolveAsync(ResolveGoogleCategoryRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(_result);
    }
}
