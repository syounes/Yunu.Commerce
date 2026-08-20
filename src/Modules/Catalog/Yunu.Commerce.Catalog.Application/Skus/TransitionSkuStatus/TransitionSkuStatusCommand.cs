namespace Yunu.Commerce.Catalog.Application.Skus.TransitionSkuStatus;

/// <summary>
/// Requests an explicit lifecycle Status transition for an existing Sku
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
/// </summary>
public sealed class TransitionSkuStatusCommand
{
    public required Guid SkuId { get; init; }

    public required string Status { get; init; }
}
