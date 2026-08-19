using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Application.Brands.DeleteBrand;

/// <summary>
/// Orchestrates deletion of a Brand not currently referenced by any Product
/// (docs task: "Canonical Taxonomy + Segments Domain" §36).
/// </summary>
public sealed class DeleteBrandHandler
{
    private readonly IBrandRepository _repository;
    private readonly IProductRepository _productRepository;

    public DeleteBrandHandler(IBrandRepository repository, IProductRepository productRepository)
    {
        _repository = repository;
        _productRepository = productRepository;
    }

    public async Task HandleAsync(DeleteBrandCommand command, CancellationToken cancellationToken)
    {
        var brandId = new BrandId(command.BrandId);
        var brand = await _repository.GetByIdAsync(brandId, cancellationToken);

        if (brand is null)
        {
            throw new KeyNotFoundException($"Brand '{command.BrandId}' not found.");
        }

        if (await _productRepository.ExistsByBrandIdAsync(brandId, cancellationToken))
        {
            throw new BrandInUseException($"Brand '{command.BrandId}' is used by at least one Product and cannot be deleted.");
        }

        await _repository.DeleteAsync(brandId, cancellationToken);
    }
}
