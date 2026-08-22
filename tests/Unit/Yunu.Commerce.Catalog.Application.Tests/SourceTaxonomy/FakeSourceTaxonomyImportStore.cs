using Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

namespace Yunu.Commerce.Catalog.Application.Tests.SourceTaxonomy;

/// <summary>
/// In-memory fake for <see cref="ISourceTaxonomyImportStore"/> used by
/// orchestrator unit tests. Tracks lifecycle transitions so tests can assert
/// Started/Failed behavior without a real database.
/// </summary>
public sealed class FakeSourceTaxonomyImportStore : ISourceTaxonomyImportStore
{
    private long _nextImportId = 1;

    public List<(long ImportId, long SourceTaxonomyId, string AdapterCode)> StartedImports { get; } = new();
    public List<(long ImportId, string ErrorMessage)> FailedImports { get; } = new();

    public Task<long> StartAsync(
        long sourceTaxonomyId,
        string adapterCode,
        string? sourceUri,
        string? externalVersion,
        string? sourceChecksum,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        var importId = _nextImportId++;
        StartedImports.Add((importId, sourceTaxonomyId, adapterCode));
        return Task.FromResult(importId);
    }

    public Task MarkFailedAsync(long importId, string errorMessage, DateTime completedAtUtc, CancellationToken cancellationToken)
    {
        FailedImports.Add((importId, errorMessage));
        return Task.CompletedTask;
    }
}
