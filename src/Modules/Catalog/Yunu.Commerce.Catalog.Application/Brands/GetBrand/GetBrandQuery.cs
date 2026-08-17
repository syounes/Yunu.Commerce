using Yunu.Commerce.Catalog.Domain.Brands;

namespace Yunu.Commerce.Catalog.Application.Brands.GetBrand;

public sealed class GetBrandQuery
{
    public required BrandId BrandId { get; init; }
}
