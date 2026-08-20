namespace Yunu.Commerce.Catalog.Application.SegmentDefinitions;

/// <summary>
/// Thrown when a Create/Update operation on a <see cref="Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinition"/>
/// would conflict with an existing Code or NormalizedName (docs task:
/// "Canonical Taxonomy + Segments Domain"), mirroring the Brand conflict
/// pattern (see <c>Yunu.Commerce.Catalog.Application.Brands.BrandInUseException</c>).
/// </summary>
public sealed class SegmentDefinitionConflictException : Exception
{
    public SegmentDefinitionConflictException(string message) : base(message)
    {
    }
}
