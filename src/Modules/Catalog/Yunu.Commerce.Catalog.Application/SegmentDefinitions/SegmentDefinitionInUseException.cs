namespace Yunu.Commerce.Catalog.Application.SegmentDefinitions;

/// <summary>
/// Thrown when an Archive transition is attempted against a
/// <see cref="Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinition"/> that
/// is still in use (docs task: "Yunu.Commerce V8 - Lifecycle + Usage Guards
/// de Segments"), mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.Brands.BrandInUseException"/>
/// and
/// <see cref="Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.CanonicalTaxonomyNodeInUseException"/>.
/// </summary>
public sealed class SegmentDefinitionInUseException : Exception
{
    public SegmentDefinitionInUseException(string message) : base(message)
    {
    }
}
