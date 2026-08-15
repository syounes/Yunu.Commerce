using Yunu.Commerce.Catalog.Application.AttributeCatalog;

namespace Yunu.Commerce.Catalog.Application.Tests;

/// <summary>
/// Test-only fake for IAttributeCatalogRepository. Exists exclusively inside
/// this test project (docs task: "SKU attribute foundation").
/// </summary>
internal sealed class FakeAttributeCatalogRepository : IAttributeCatalogRepository
{
    private readonly Dictionary<string, AttributeDefinitionResponse> _definitionsByCode = new();
    private readonly Dictionary<int, AttributeDefinitionResponse> _definitionsById = new();
    private readonly Dictionary<(int DefinitionId, string OptionCode), AttributeOptionResponse> _options = new();

    public void AddDefinition(AttributeDefinitionResponse definition)
    {
        _definitionsByCode[definition.Code] = definition;
        _definitionsById[definition.AttributeDefinitionId] = definition;
    }

    public void AddOption(AttributeOptionResponse option)
    {
        _options[(option.AttributeDefinitionId, option.Code)] = option;
    }

    public Task<AttributeDefinitionResponse?> GetDefinitionByCodeAsync(string code, CancellationToken cancellationToken)
    {
        _definitionsByCode.TryGetValue(code, out var definition);
        return Task.FromResult(definition);
    }

    public Task<AttributeDefinitionResponse?> GetDefinitionByIdAsync(int attributeDefinitionId, CancellationToken cancellationToken)
    {
        _definitionsById.TryGetValue(attributeDefinitionId, out var definition);
        return Task.FromResult(definition);
    }

    public Task<AttributeOptionResponse?> GetOptionAsync(int attributeDefinitionId, string optionCode, CancellationToken cancellationToken)
    {
        _options.TryGetValue((attributeDefinitionId, optionCode), out var option);
        return Task.FromResult(option);
    }
}
