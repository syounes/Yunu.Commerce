namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy;

/// <summary>
/// Thrown when creation of a child
/// <see cref="Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy.CanonicalTaxonomyNode"/>
/// is attempted under a parent node that is Archived (docs task:
/// "Yunu.Commerce V9 - Canonical Taxonomy Lifecycle + Usage Guards"),
/// mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.SegmentOptions.SegmentDefinitionArchivedException"/>.
/// </summary>
public sealed class CanonicalTaxonomyNodeParentArchivedException : Exception
{
    public CanonicalTaxonomyNodeParentArchivedException(string message) : base(message)
    {
    }
}
