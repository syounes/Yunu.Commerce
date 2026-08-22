using Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

namespace Yunu.Commerce.Catalog.Application.Tests.SourceTaxonomy;

/// <summary>
/// In-memory fake for <see cref="ISourceTaxonomySynchronizationStore"/> used
/// by orchestrator unit tests. Returns a fixed result and records the last
/// call arguments.
/// </summary>
public sealed class FakeSourceTaxonomySynchronizationStore : ISourceTaxonomySynchronizationStore
{
    private readonly SourceTaxonomySynchronizationResult _result;
    private readonly Exception? _exceptionToThrow;

    public FakeSourceTaxonomySynchronizationStore(SourceTaxonomySynchronizationResult result)
    {
        _result = result;
    }

    public FakeSourceTaxonomySynchronizationStore(Exception exceptionToThrow)
    {
        _exceptionToThrow = exceptionToThrow;
        _result = null!;
    }

    public long? LastImportId { get; private set; }
    public long? LastSourceTaxonomyId { get; private set; }

    public Task<SourceTaxonomySynchronizationResult> ApplyAsync(
        long sourceTaxonomyId,
        long importId,
        SourceTaxonomySnapshot snapshot,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        LastSourceTaxonomyId = sourceTaxonomyId;
        LastImportId = importId;

        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }

        return Task.FromResult(_result);
    }
}
