namespace Yunu.Commerce.Catalog.Application.Products;

/// <summary>
/// Thrown when an Archive transition is attempted against a
/// <see cref="Yunu.Commerce.Catalog.Domain.Products.Product"/> that still
/// has at least one non-Archived Sku
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md),
/// mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.SegmentDefinitions.SegmentDefinitionInUseException"/>.
/// </summary>
public sealed class ProductHasNonArchivedSkusException : Exception
{
    public ProductHasNonArchivedSkusException(string message) : base(message)
    {
    }
}
