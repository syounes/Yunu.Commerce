using Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

namespace Yunu.Commerce.Catalog.Application.Tests.SourceTaxonomy;

/// <summary>
/// Fake <see cref="ISourceTaxonomyAdapter"/> for orchestrator unit tests.
/// Records the last <see cref="SourceTaxonomyImportContext"/> it was called
/// with, so tests can assert the orchestrator built a correct,
/// provider-neutral context.
/// </summary>
public sealed class FakeSourceTaxonomyAdapter : ISourceTaxonomyAdapter
{
    private readonly Func<SourceTaxonomyImportContext, SourceTaxonomySnapshot> _snapshotFactory;
    private readonly Exception? _exceptionToThrow;

    public FakeSourceTaxonomyAdapter(string adapterCode, Func<SourceTaxonomyImportContext, SourceTaxonomySnapshot> snapshotFactory)
    {
        AdapterCode = adapterCode;
        _snapshotFactory = snapshotFactory;
    }

    public FakeSourceTaxonomyAdapter(string adapterCode, Exception exceptionToThrow)
    {
        AdapterCode = adapterCode;
        _snapshotFactory = _ => throw exceptionToThrow;
        _exceptionToThrow = exceptionToThrow;
    }

    public string AdapterCode { get; }

    public SourceTaxonomyImportContext? LastContext { get; private set; }

    public Task<SourceTaxonomySnapshot> LoadAsync(SourceTaxonomyImportContext context, CancellationToken cancellationToken)
    {
        LastContext = context;

        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }

        return Task.FromResult(_snapshotFactory(context));
    }
}
