using Yunu.Commerce.Catalog.Application.Skus.CreateSku;

namespace Yunu.Commerce.Api.Skus;

/// <summary>
/// HTTP request contract for creating a Sku. SkuId is intentionally absent:
/// identity is generated inside Catalog.Application. Attributes are optional
/// and explicit only (docs task: "SKU attribute foundation"): the caller must
/// send explicit attribute codes and values/option codes; natural-language
/// interpretation is out of scope for this stage.
/// </summary>
public sealed class CreateSkuRequest
{
    public required string Code { get; init; }

    public string? Gtin { get; init; }

    public IReadOnlyCollection<SkuAttributeRequest>? Attributes { get; init; }
}
