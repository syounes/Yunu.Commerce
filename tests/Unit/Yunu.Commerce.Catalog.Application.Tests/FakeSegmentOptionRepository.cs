using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Application.Tests;

internal sealed class FakeSegmentOptionRepository : ISegmentOptionRepository
{
    private readonly Dictionary<long, SegmentOption> _options = new();
    private long _nextId = 1;

    public Task<SegmentOptionId> AddAsync(SegmentOption option, CancellationToken cancellationToken)
    {
        if (option.Id is not null)
        {
            throw new InvalidOperationException("Cannot add a SegmentOption that already has an identity.");
        }

        var id = new SegmentOptionId(_nextId++);
        option.AssignIdentity(id);
        _options[id.Value] = option;

        return Task.FromResult(id);
    }

    public Task UpdateAsync(SegmentOption option, CancellationToken cancellationToken)
    {
        if (option.Id is not { } id)
        {
            throw new InvalidOperationException("Cannot update a SegmentOption without an identity.");
        }

        if (!_options.ContainsKey(id.Value))
        {
            throw new InvalidOperationException($"SegmentOption '{id.Value}' was not found.");
        }

        _options[id.Value] = option;
        return Task.CompletedTask;
    }

    public Task<SegmentOption?> GetByIdAsync(SegmentOptionId id, CancellationToken cancellationToken)
    {
        _options.TryGetValue(id.Value, out var option);
        return Task.FromResult(option);
    }

    public Task<SegmentOption?> GetByCodeAsync(SegmentDefinitionId segmentDefinitionId, SegmentOptionCode code, CancellationToken cancellationToken)
    {
        var found = _options.Values.FirstOrDefault(o =>
            o.SegmentDefinitionId == segmentDefinitionId && o.Code.Value == code.Value);
        return Task.FromResult(found);
    }

    public Task<SegmentOption?> FindByNormalizedNameAsync(SegmentDefinitionId segmentDefinitionId, string normalizedName, CancellationToken cancellationToken)
    {
        var found = _options.Values.FirstOrDefault(o =>
            o.SegmentDefinitionId == segmentDefinitionId && o.NormalizedName == normalizedName);
        return Task.FromResult(found);
    }

    public Task<bool> ExistsCodeAsync(SegmentDefinitionId segmentDefinitionId, SegmentOptionCode code, CancellationToken cancellationToken)
    {
        var exists = _options.Values.Any(o =>
            o.SegmentDefinitionId == segmentDefinitionId && o.Code.Value == code.Value);
        return Task.FromResult(exists);
    }
}
