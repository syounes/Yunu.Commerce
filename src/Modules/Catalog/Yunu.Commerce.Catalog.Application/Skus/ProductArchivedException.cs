namespace Yunu.Commerce.Catalog.Application.Skus;

/// <summary>
/// Thrown when a Sku (re)activation/blocking transition is attempted while
/// its owning Product is Archived
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
/// A Sku under an Archived Product may only remain/become Archived.
/// </summary>
public sealed class ProductArchivedException : Exception
{
    public ProductArchivedException(string message) : base(message)
    {
    }
}
