using Yunu.Commerce.Catalog.Application.SegmentCatalog;

namespace Yunu.Commerce.Catalog.Application.Tests;

/// <summary>
/// Test-only fake for ISegmentCatalogRepository.
/// </summary>
internal sealed class FakeSegmentCatalogRepository : ISegmentCatalogRepository
{
    private readonly Dictionary<long, SegmentDefinitionResponse> _definitionsById = new();
    private readonly Dictionary<string, SegmentDefinitionResponse> _definitionsByCode = new();
    private readonly Dictionary<(long, string), SegmentOptionResponse> _options = new();

    public Task<SegmentDefinitionResponse?> GetDefinitionByCodeAsync(string code, CancellationToken cancellationToken)
    {
        _definitionsByCode.TryGetValue(code, out var definition);
        return Task.FromResult(definition);
    }

    public Task<SegmentDefinitionResponse?> GetDefinitionByIdAsync(long segmentDefinitionId, CancellationToken cancellationToken)
    {
        _definitionsById.TryGetValue(segmentDefinitionId, out var definition);
        return Task.FromResult(definition);
    }

    public Task<IReadOnlyCollection<SegmentDefinitionResponse>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<SegmentDefinitionResponse>>(_definitionsById.Values.ToList());
    }

    public Task<SegmentOptionResponse?> GetOptionAsync(long segmentDefinitionId, string optionCode, CancellationToken cancellationToken)
    {
        _options.TryGetValue((segmentDefinitionId, optionCode), out var option);
        return Task.FromResult(option);
    }

    public Task<IReadOnlyCollection<SegmentOptionResponse>> GetOptionsByDefinitionAsync(long segmentDefinitionId, CancellationToken cancellationToken)
    {
        var options = _options.Values.Where(o => o.SegmentDefinitionId == segmentDefinitionId).ToList();
        return Task.FromResult<IReadOnlyCollection<SegmentOptionResponse>>(options);
    }

    public void AddDefinition(SegmentDefinitionResponse definition)
    {
        _definitionsById[definition.SegmentDefinitionId] = definition;
        _definitionsByCode[definition.Code] = definition;
    }

    public void AddOption(SegmentOptionResponse option)
    {
        _options[(option.SegmentDefinitionId, option.Code)] = option;
    }
}
