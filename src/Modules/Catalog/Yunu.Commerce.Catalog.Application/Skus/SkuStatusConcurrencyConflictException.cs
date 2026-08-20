namespace Yunu.Commerce.Catalog.Application.Skus;

/// <summary>
/// Thrown when a Sku lifecycle Status transition command cannot be applied
/// because the Aggregate was concurrently changed by another writer
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
/// Mirrors <see cref="Yunu.Commerce.Catalog.Application.Products.ProductStatusConcurrencyConflictException"/>:
/// no reload-and-reinterpret is attempted; the HTTP layer translates this
/// into 409 Conflict.
/// </summary>
public sealed class SkuStatusConcurrencyConflictException : Exception
{
    public SkuStatusConcurrencyConflictException(string message) : base(message)
    {
    }
}
