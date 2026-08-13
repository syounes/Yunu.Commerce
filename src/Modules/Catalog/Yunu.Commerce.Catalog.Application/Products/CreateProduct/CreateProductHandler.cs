using Yunu.Commerce.Catalog.Application.GoogleTaxonomy;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Families;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Application.Products.CreateProduct;

/// <summary>
/// Orchestrates creation of a new Product Aggregate (docs/domains/catalog.md §49,
/// docs/adr/0001-use-ddd-clean-hexagonal.md §8). Business invariants are enforced
/// entirely by Catalog.Domain (Product.Create and its Value Objects); this handler
/// performs only mapping, Google taxonomy resolution and persistence orchestration.
///
/// GoogleCategory is a required external classification. The canonical category
/// (id + full path) is resolved from <see cref="IGoogleTaxonomyRepository"/>
/// (backed by SQL Server) BEFORE the Product Aggregate is constructed; the Domain
/// never performs this lookup itself. Only active, leaf categories are accepted.
///
/// Domain Events raised during creation (ProductCreatedDomainEvent) remain in the
/// Aggregate's event collection and are not dispatched or cleared at this phase;
/// no Integration Event or Outbox mechanism exists yet.
/// </summary>
public sealed class CreateProductHandler
{
    private readonly IProductRepository _productRepository;
    private readonly IGoogleTaxonomyRepository _googleTaxonomyRepository;

    public CreateProductHandler(IProductRepository productRepository, IGoogleTaxonomyRepository googleTaxonomyRepository)
    {
        _productRepository = productRepository;
        _googleTaxonomyRepository = googleTaxonomyRepository;
    }

    public async Task<CreateProductResult> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var productId = ProductId.New();
        var name = new ProductName(command.Name);
        var brandId = command.BrandId is { } brandIdValue ? new BrandId(brandIdValue) : (BrandId?)null;
        var familyId = command.FamilyId is { } familyIdValue ? new FamilyId(familyIdValue) : (FamilyId?)null;

        var googleCategory = await ResolveGoogleCategoryAsync(command.GoogleCategoryId, cancellationToken);

        var product = Product.Create(productId, name, command.Description, brandId, familyId, googleCategory);

        await _productRepository.AddAsync(product, cancellationToken);

        return new CreateProductResult
        {
            ProductId = productId.Value
        };
    }

    private async Task<GoogleCategoryReference> ResolveGoogleCategoryAsync(int googleCategoryId, CancellationToken cancellationToken)
    {
        var category = await _googleTaxonomyRepository.GetByIdAsync(googleCategoryId, cancellationToken);

        if (category is null)
        {
            throw new ArgumentException($"Google category '{googleCategoryId}' does not exist.", nameof(googleCategoryId));
        }

        if (!category.IsActive)
        {
            throw new ArgumentException($"Google category '{googleCategoryId}' is not active.", nameof(googleCategoryId));
        }

        if (!category.IsLeaf)
        {
            throw new ArgumentException($"Google category '{googleCategoryId}' is not a leaf category.", nameof(googleCategoryId));
        }

        return new GoogleCategoryReference(category.GoogleCategoryId, category.FullPath);
    }
}
