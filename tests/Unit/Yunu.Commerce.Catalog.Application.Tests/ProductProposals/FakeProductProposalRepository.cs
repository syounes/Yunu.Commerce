using Yunu.Commerce.Catalog.Domain.ProductProposals;

namespace Yunu.Commerce.Catalog.Application.Tests.ProductProposals;

/// <summary>
/// Test-only fake for IProductProposalRepository. Exists exclusively inside
/// this test project; no production InMemoryProductProposalRepository is
/// introduced at this phase.
/// </summary>
internal sealed class FakeProductProposalRepository : IProductProposalRepository
{
    private readonly Dictionary<Guid, ProductProposal> _proposals = new();

    public int AddAsyncCallCount { get; private set; }

    public ProductProposal? LastAdded { get; private set; }

    public Task AddAsync(ProductProposal proposal, CancellationToken cancellationToken)
    {
        AddAsyncCallCount++;
        LastAdded = proposal;
        _proposals[proposal.Id.Value] = proposal;
        return Task.CompletedTask;
    }

    public Task<ProductProposal?> GetByIdAsync(ProductProposalId id, CancellationToken cancellationToken)
    {
        _proposals.TryGetValue(id.Value, out var proposal);
        return Task.FromResult(proposal);
    }
}
