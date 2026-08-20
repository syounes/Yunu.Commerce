namespace Yunu.Commerce.Catalog.Application.Products.TransitionProductStatus;

/// <summary>
/// Requests an explicit lifecycle Status transition for an existing Product
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
/// </summary>
public sealed class TransitionProductStatusCommand
{
    public required Guid ProductId { get; init; }

    public required string Status { get; init; }
}
