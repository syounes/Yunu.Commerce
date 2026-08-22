namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Provider-neutral adapter contract (docs/adr/0014-provider-neutral-source-taxonomy.md
/// §9). Translates one upstream provider-native taxonomy into a normalized
/// <see cref="SourceTaxonomySnapshot"/>. No provider SDK type may escape
/// through this interface; the generic import orchestrator and SQL
/// synchronization never see provider-native shapes.
///
/// <see cref="AdapterCode"/> is an open string and is intentionally NOT
/// required to equal a SourceTaxonomy's ProviderCode: a single provider may
/// eventually be served by more than one adapter implementation (for
/// example a future "google-product-taxonomy" adapter for ProviderCode
/// "google"). No enum of adapters/providers exists by design.
/// </summary>
public interface ISourceTaxonomyAdapter
{
    string AdapterCode { get; }

    Task<SourceTaxonomySnapshot> LoadAsync(
        SourceTaxonomyImportContext context,
        CancellationToken cancellationToken);
}
