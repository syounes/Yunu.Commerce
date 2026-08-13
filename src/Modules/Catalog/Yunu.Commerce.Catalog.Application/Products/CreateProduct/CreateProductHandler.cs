using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Categories;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Application.Products.CreateProduct;

/// <summary>
/// Orchestrates creation of a new Product Aggregate (docs/domains/catalog.md §49,
/// docs/adr/0001-use-ddd-clean-hexagonal.md §8). Business invariants are enforced
/// entirely by Catalog.Domain (Product.Create and its Value Objects); this handler
/// performs only mapping and persistence orchestration.
///
/// Domain Events raised during creation (ProductCreatedDomainEvent) remain in the
/// Aggregate's event collection and are not dispatched or cleared at this phase;
/// no Integration Event or Outbox mechanism exists yet.
/// </summary>
public sealed class CreateProductHandler
{
    private readonly IProductRepository _productRepository;

    public CreateProductHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<CreateProductResult> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var productId = ProductId.New();
        var name = new ProductName(command.Name);
        var brandId = new BrandId(command.BrandId);
        var categoryId = new CategoryId(command.CategoryId);

        var product = Product.Create(productId, name, command.Description, brandId, categoryId);

        await _productRepository.AddAsync(product, cancellationToken);

        return new CreateProductResult
        {
            ProductId = productId.Value
        };
    }
}
