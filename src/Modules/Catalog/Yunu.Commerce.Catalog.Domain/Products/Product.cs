using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Categories;
using Yunu.Commerce.Catalog.Domain.Products.Events;
using Yunu.Commerce.Catalog.Domain.Products.Skus;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Products;

/// <summary>
/// Product Aggregate Root (docs/domains/catalog.md §4-§6).
/// Owns the canonical descriptive identity of a commercial product and its Skus.
///
/// Modeling decision: Sku is implemented as an Entity owned by this Aggregate for
/// this initial slice (docs/domains/catalog.md §46-§47). This is a first-cut
/// decision, not guaranteed to remain non-breaking if revisited: extracting Sku
/// into an independent Aggregate later could require changes to transactional
/// consistency boundaries, repository contracts, Domain Events, and Application
/// use case flows.
///
/// Lifecycle transition behavior (Activate/Deactivate/Archive/SubmitForReview) is
/// intentionally deferred until a documented use case defines the transition rules
/// (docs/domains/catalog.md §21).
/// </summary>
public sealed class Product
{
    private readonly List<Sku> _skus = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    public ProductId Id { get; }

    public ProductName Name { get; private set; }

    public BrandId BrandId { get; }

    public CategoryId CategoryId { get; }

    public ProductStatus Status { get; private set; }

    public IReadOnlyCollection<Sku> Skus => _skus;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    private Product(ProductId id, ProductName name, BrandId brandId, CategoryId categoryId, ProductStatus status)
    {
        Id = id;
        Name = name;
        BrandId = brandId;
        CategoryId = categoryId;
        Status = status;
    }

    public static Product Create(
        ProductId id,
        ProductName name,
        BrandId brandId,
        CategoryId categoryId,
        ProductStatus status = ProductStatus.Draft)
    {
        var product = new Product(id, name, brandId, categoryId, status);

        product._domainEvents.Add(new ProductCreatedDomainEvent(id));

        return product;
    }

    /// <summary>
    /// Renames the Product. Raises <see cref="ProductRenamedDomainEvent"/> only when
    /// the new name is different from the current name (docs/domains/catalog.md §38).
    /// </summary>
    public void Rename(ProductName newName)
    {
        ArgumentNullException.ThrowIfNull(newName);

        if (Name == newName)
        {
            return;
        }

        var previousName = Name;
        Name = newName;

        _domainEvents.Add(new ProductRenamedDomainEvent(Id, previousName, newName));
    }

    /// <summary>
    /// Adds a Sku to this Product. Duplicate Sku codes are not validated in this
    /// phase because Sku uniqueness rules are not yet documented
    /// (docs/domains/catalog.md §22).
    /// </summary>
    public Sku AddSku(SkuId id, SkuCode code, SkuStatus status)
    {
        var sku = new Sku(id, code, status);

        _skus.Add(sku);
        _domainEvents.Add(new SkuAddedDomainEvent(Id, id));

        return sku;
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
