namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;

/// <summary>
/// Input for triggering a Google Product Taxonomy synchronization. Empty for
/// now: source URL and language come from <see cref="GoogleTaxonomyOptions"/>.
/// Kept as an explicit command type so future parameters (e.g. an override
/// URL for testing) do not require changing the handler signature.
/// </summary>
public sealed class SynchronizeGoogleTaxonomyCommand
{
}
