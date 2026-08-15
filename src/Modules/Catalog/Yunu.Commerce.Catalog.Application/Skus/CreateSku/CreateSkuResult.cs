using Yunu.Commerce.Catalog.Application.Skus;

namespace Yunu.Commerce.Catalog.Application.Skus.CreateSku;

/// <summary>
/// Minimal confirmation returned after a Sku is created, including the
/// attributes that were actually assigned (docs task: "SKU attribute
/// foundation").
/// </summary>
public sealed class CreateSkuResult
{
    public required Guid SkuId { get; init; }

    public IReadOnlyCollection<SkuAttributeResponse> Attributes { get; init; } = Array.Empty<SkuAttributeResponse>();
}
