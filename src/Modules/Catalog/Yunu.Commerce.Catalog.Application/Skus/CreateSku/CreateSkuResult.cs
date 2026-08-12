namespace Yunu.Commerce.Catalog.Application.Skus.CreateSku;

/// <summary>
/// Minimal confirmation returned after a Sku is created.
/// </summary>
public sealed class CreateSkuResult
{
    public required Guid SkuId { get; init; }
}
