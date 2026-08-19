namespace Yunu.Commerce.Catalog.Application.Brands.DeleteBrand;

public sealed class DeleteBrandCommand
{
    public required Guid BrandId { get; init; }
}
