using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Application.Tests;

internal sealed class FakeSegmentDefinitionRepository : ISegmentDefinitionRepository
{
    private readonly Dictionary<long, SegmentDefinition> _definitions = new();
    private long _nextId = 1;

    public Task<SegmentDefinitionId> AddAsync(SegmentDefinition definition, CancellationToken cancellationToken)
    {
        if (definition.Id is not null)
        {
            throw new InvalidOperationException("Cannot add a SegmentDefinition that already has an identity.");
        }

        var id = new SegmentDefinitionId(_nextId++);
        definition.AssignIdentity(id);
        _definitions[id.Value] = definition;

        return Task.FromResult(id);
    }

    public Task UpdateAsync(SegmentDefinition definition, CancellationToken cancellationToken)
    {
        if (definition.Id is not { } id)
        {
            throw new InvalidOperationException("Cannot update a SegmentDefinition without an identity.");
        }

        if (!_definitions.ContainsKey(id.Value))
        {
            throw new InvalidOperationException($"SegmentDefinition '{id.Value}' was not found.");
        }

        _definitions[id.Value] = definition;
        return Task.CompletedTask;
    }

    public Task<SegmentDefinition?> GetByIdAsync(SegmentDefinitionId id, CancellationToken cancellationToken)
    {
        _definitions.TryGetValue(id.Value, out var definition);
        return Task.FromResult(definition);
    }

    public Task<SegmentDefinition?> GetByCodeAsync(SegmentDefinitionCode code, CancellationToken cancellationToken)
    {
        var found = _definitions.Values.FirstOrDefault(d => d.Code.Value == code.Value);
        return Task.FromResult(found);
    }

    public Task<SegmentDefinition?> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken)
    {
        var found = _definitions.Values.FirstOrDefault(d => d.NormalizedName == normalizedName);
        return Task.FromResult(found);
    }

    public Task<bool> ExistsCodeAsync(SegmentDefinitionCode code, CancellationToken cancellationToken)
    {
        var exists = _definitions.Values.Any(d => d.Code.Value == code.Value);
        return Task.FromResult(exists);
    }
}
