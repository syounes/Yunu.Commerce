using Yunu.Commerce.Catalog.Application.Skus;

namespace Yunu.Commerce.Api.Skus;

/// <summary>
/// HTTP response contract returned after a Sku is created, including the
/// attributes that were actually assigned (docs task: "SKU attribute
/// foundation").
/// </summary>
public sealed class CreateSkuResponse
{
    public required Guid SkuId { get; init; }

    public IReadOnlyCollection<SkuAttributeResponse> Attributes { get; init; } = Array.Empty<SkuAttributeResponse>();
}
