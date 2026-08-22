using System.Collections.Concurrent;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

namespace Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.Synchronization.InMemory;

/// <summary>
/// Process-local (single-instance) implementation of
/// <see cref="ISourceTaxonomyImportGuard"/>, keyed by SourceTaxonomyId so
/// unrelated SourceTaxonomies can be imported concurrently
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §18). A distributed
/// lock (e.g. Redis) can replace this later without touching the
/// orchestrator.
/// </summary>
public sealed class InMemorySourceTaxonomyImportGuard : ISourceTaxonomyImportGuard
{
    private readonly ConcurrentDictionary<long, byte> _runningImports = new();

    public IDisposable? TryAcquire(long sourceTaxonomyId)
    {
        return _runningImports.TryAdd(sourceTaxonomyId, 0)
            ? new ReleaseToken(this, sourceTaxonomyId)
            : null;
    }

    private sealed class ReleaseToken : IDisposable
    {
        private readonly InMemorySourceTaxonomyImportGuard _guard;
        private readonly long _sourceTaxonomyId;

        public ReleaseToken(InMemorySourceTaxonomyImportGuard guard, long sourceTaxonomyId)
        {
            _guard = guard;
            _sourceTaxonomyId = sourceTaxonomyId;
        }

        public void Dispose()
        {
            _guard._runningImports.TryRemove(_sourceTaxonomyId, out _);
        }
    }
}
