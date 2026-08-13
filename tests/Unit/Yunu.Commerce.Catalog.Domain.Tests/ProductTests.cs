using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Families;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Products.Events;
using Xunit;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class ProductTests
{
    private static GoogleCategoryReference CreateGoogleCategory()
    {
        return new GoogleCategoryReference(1234, "Apparel & Accessories > Shoes > Athletic Shoes");
    }

    private static Product CreateProduct(
        string name = "Apple iPhone 17 Pro",
        string? description = null,
        BrandId? brandId = null,
        FamilyId? familyId = null,
        GoogleCategoryReference? googleCategory = null)
    {
        return Product.Create(
            ProductId.New(),
            new ProductName(name),
            description,
            brandId,
            familyId,
            googleCategory ?? CreateGoogleCategory());
    }

    [Fact]
    public void Create_Should_Default_To_Draft_Status()
    {
        var product = CreateProduct();

        Assert.Equal(ProductStatus.Draft, product.Status);
    }

    [Fact]
    public void Create_Should_Raise_ProductCreatedDomainEvent()
    {
        var product = CreateProduct();

        var domainEvent = Assert.Single(product.DomainEvents);
        Assert.IsType<ProductCreatedDomainEvent>(domainEvent);
        Assert.Equal(product.Id, ((ProductCreatedDomainEvent)domainEvent).ProductId);
    }

    [Fact]
    public void Create_With_Description_Should_Set_Description()
    {
        var product = CreateProduct(description: "Tênis esportivo masculino para corrida e uso diário.");

        Assert.Equal("Tênis esportivo masculino para corrida e uso diário.", product.Description);
    }

    [Fact]
    public void Create_Without_Description_Should_Leave_Description_Null()
    {
        var product = CreateProduct();

        Assert.Null(product.Description);
    }

    [Fact]
    public void Create_Should_Require_GoogleCategory()
    {
        Assert.Throws<ArgumentNullException>(() => Product.Create(
            ProductId.New(),
            new ProductName("Apple iPhone 17 Pro"),
            description: null,
            brandId: null,
            familyId: null,
            googleCategory: null!));
    }

    [Fact]
    public void Create_Should_Accept_Null_BrandId()
    {
        var product = CreateProduct(brandId: null);

        Assert.Null(product.BrandId);
    }

    [Fact]
    public void Create_Should_Accept_Null_FamilyId()
    {
        var product = CreateProduct(familyId: null);

        Assert.Null(product.FamilyId);
    }

    [Fact]
    public void Create_Should_Accept_Both_BrandId_And_FamilyId()
    {
        var brandId = BrandId.New();
        var familyId = FamilyId.New();

        var product = CreateProduct(brandId: brandId, familyId: familyId);

        Assert.Equal(brandId, product.BrandId);
        Assert.Equal(familyId, product.FamilyId);
    }

    [Fact]
    public void Create_Should_Store_GoogleCategory()
    {
        var googleCategory = CreateGoogleCategory();

        var product = CreateProduct(googleCategory: googleCategory);

        Assert.Equal(googleCategory, product.GoogleCategory);
    }

    [Fact]
    public void Rename_With_Different_Name_Should_Update_Name_And_Raise_Event()
    {
        var product = CreateProduct();
        product.ClearDomainEvents();

        var newName = new ProductName("Apple iPhone 17 Pro Max");
        product.Rename(newName);

        Assert.Equal(newName, product.Name);

        var domainEvent = Assert.Single(product.DomainEvents);
        var renamedEvent = Assert.IsType<ProductRenamedDomainEvent>(domainEvent);
        Assert.Equal(product.Id, renamedEvent.ProductId);
        Assert.Equal(newName, renamedEvent.NewName);
    }

    [Fact]
    public void Rename_With_Same_Name_Should_Not_Raise_Event()
    {
        var product = CreateProduct("Apple iPhone 17 Pro");
        product.ClearDomainEvents();

        product.Rename(new ProductName("Apple iPhone 17 Pro"));

        Assert.Empty(product.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_Should_Empty_Collection()
    {
        var product = CreateProduct();

        Assert.NotEmpty(product.DomainEvents);

        product.ClearDomainEvents();

        Assert.Empty(product.DomainEvents);
    }
}
