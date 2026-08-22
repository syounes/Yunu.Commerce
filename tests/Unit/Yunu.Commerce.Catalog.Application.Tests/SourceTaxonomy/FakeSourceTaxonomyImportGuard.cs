using System.Collections.Concurrent;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

namespace Yunu.Commerce.Catalog.Application.Tests.SourceTaxonomy;

/// <summary>
/// Simple in-memory keyed guard fake mirroring
/// InMemorySourceTaxonomyImportGuard, for orchestrator unit tests that don't
/// need Infrastructure.
/// </summary>
public sealed class FakeSourceTaxonomyImportGuard : ISourceTaxonomyImportGuard
{
    private readonly ConcurrentDictionary<long, byte> _running = new();

    public IDisposable? TryAcquire(long sourceTaxonomyId)
        => _running.TryAdd(sourceTaxonomyId, 0) ? new ReleaseToken(this, sourceTaxonomyId) : null;

    private sealed class ReleaseToken : IDisposable
    {
        private readonly FakeSourceTaxonomyImportGuard _guard;
        private readonly long _id;

        public ReleaseToken(FakeSourceTaxonomyImportGuard guard, long id)
        {
            _guard = guard;
            _id = id;
        }

        public void Dispose() => _guard._running.TryRemove(_id, out _);
    }
}
