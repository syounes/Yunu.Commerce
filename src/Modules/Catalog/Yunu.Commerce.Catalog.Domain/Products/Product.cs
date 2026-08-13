using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Families;
using Yunu.Commerce.Catalog.Domain.Products.Events;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Products;

/// <summary>
/// Product Aggregate Root (docs/domains/catalog.md §4-§6).
/// Owns the canonical descriptive identity of a commercial product.
///
/// Modeling decision (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md):
/// Sku is now an independent Aggregate Root that references this Product only by
/// <see cref="ProductId"/>. Product no longer owns, constructs or persists Sku
/// state; composition of Product + Skus for read purposes belongs to the
/// Application/read-model layer, not to this Aggregate.
///
/// Classification modeling decision: Product no longer owns an internal
/// CategoryId directly. GoogleCategory (an external, required classification
/// resolved by Application from the canonical Google Product Taxonomy) is now
/// the mandatory classification at Product creation time. BrandId and FamilyId
/// (the internal Yunu Department → Category → SubCategory → Family hierarchy
/// reference) remain optional, because internal Yunu classification/mapping may
/// be assigned after creation, once Brand/Family enrichment is implemented.
///
/// Lifecycle transition behavior (Activate/Deactivate/Archive/SubmitForReview) is
/// intentionally deferred until a documented use case defines the transition rules
/// (docs/domains/catalog.md §21).
/// </summary>
public sealed class Product
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public ProductId Id { get; }

    public ProductName Name { get; private set; }

    /// <summary>
    /// Optional free-text description of the Product. Kept as a plain string
    /// (not a Value Object) because no validation/business rule currently
    /// justifies one; introduce one later only if a documented rule requires it.
    /// </summary>
    public string? Description { get; private set; }

    public BrandId? BrandId { get; }

    public FamilyId? FamilyId { get; }

    public GoogleCategoryReference GoogleCategory { get; }

    public ProductStatus Status { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    private Product(
        ProductId id,
        ProductName name,
        string? description,
        BrandId? brandId,
        FamilyId? familyId,
        GoogleCategoryReference googleCategory,
        ProductStatus status)
    {
        ArgumentNullException.ThrowIfNull(googleCategory);

        Id = id;
        Name = name;
        Description = description;
        BrandId = brandId;
        FamilyId = familyId;
        GoogleCategory = googleCategory;
        Status = status;
    }

    public static Product Create(
        ProductId id,
        ProductName name,
        string? description,
        BrandId? brandId,
        FamilyId? familyId,
        GoogleCategoryReference googleCategory,
        ProductStatus status = ProductStatus.Draft)
    {
        var product = new Product(id, name, description, brandId, familyId, googleCategory, status);

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

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
