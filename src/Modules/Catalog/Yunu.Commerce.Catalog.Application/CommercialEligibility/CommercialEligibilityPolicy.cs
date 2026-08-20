using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.CommercialEligibility;

/// <summary>
/// Deterministic, read-only policy computing whether a given Product/Sku pair
/// is commercially eligible (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
///
/// Commercial Eligibility is composed, not inherited: it is
/// <c>Product.Status == Active &amp;&amp; Sku.Status == Active</c>. It is never
/// persisted, and Product/Sku Status is never mutated or propagated by this
/// policy; it is computed on demand exclusively for read models.
/// </summary>
public static class CommercialEligibilityPolicy
{
    public static bool IsEligible(ProductStatus productStatus, SkuStatus skuStatus)
    {
        return productStatus == ProductStatus.Active && skuStatus == SkuStatus.Active;
    }
}
