namespace Yunu.Commerce.Catalog.Application.Brands.UpdateBrand;

public sealed class UpdateBrandCommand
{
    public required System.Guid BrandId { get; init; }

    public string? Name { get; init; }

    public string? Status { get; init; }
}
