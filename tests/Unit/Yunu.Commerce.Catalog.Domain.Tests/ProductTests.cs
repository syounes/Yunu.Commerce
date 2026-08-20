using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Products.Events;
using Xunit;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class ProductTests
{
    private static CanonicalTaxonomyNodeId CreateCanonicalTaxonomyNodeId()
    {
        return new CanonicalTaxonomyNodeId(1234);
    }

    private static Product CreateProduct(
        string name = "Apple iPhone 17 Pro",
        string? description = null,
        BrandId? brandId = null,
        CanonicalTaxonomyNodeId? canonicalTaxonomyNodeId = null)
    {
        return Product.Create(
            ProductId.New(),
            new ProductName(name),
            description,
            brandId,
            canonicalTaxonomyNodeId ?? CreateCanonicalTaxonomyNodeId());
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
    public void Create_Should_Accept_Null_BrandId()
    {
        var product = CreateProduct(brandId: null);

        Assert.Null(product.BrandId);
    }

    [Fact]
    public void Create_Should_Store_CanonicalTaxonomyNodeId()
    {
        var canonicalTaxonomyNodeId = CreateCanonicalTaxonomyNodeId();

        var product = CreateProduct(canonicalTaxonomyNodeId: canonicalTaxonomyNodeId);

        Assert.Equal(canonicalTaxonomyNodeId, product.CanonicalTaxonomyNodeId);
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

    [Theory]
    [InlineData(ProductStatus.Draft, ProductStatus.Active)]
    [InlineData(ProductStatus.Draft, ProductStatus.Archived)]
    [InlineData(ProductStatus.Active, ProductStatus.Inactive)]
    [InlineData(ProductStatus.Active, ProductStatus.Archived)]
    [InlineData(ProductStatus.Inactive, ProductStatus.Active)]
    [InlineData(ProductStatus.Inactive, ProductStatus.Archived)]
    public void TransitionTo_allows_documented_transitions(ProductStatus from, ProductStatus to)
    {
        var product = CreateProduct();

        if (from != ProductStatus.Draft)
        {
            product.TransitionTo(ProductStatus.Active);
            if (from == ProductStatus.Inactive)
            {
                product.TransitionTo(ProductStatus.Inactive);
            }
        }

        product.TransitionTo(to);

        Assert.Equal(to, product.Status);
    }

    [Theory]
    [InlineData(ProductStatus.Archived, ProductStatus.Draft)]
    [InlineData(ProductStatus.Archived, ProductStatus.Active)]
    [InlineData(ProductStatus.Archived, ProductStatus.Inactive)]
    public void TransitionTo_from_archived_always_throws(ProductStatus from, ProductStatus to)
    {
        var product = CreateProduct();
        product.TransitionTo(ProductStatus.Archived);

        Assert.Equal(ProductStatus.Archived, from);

        Assert.Throws<InvalidProductStatusTransitionException>(() => product.TransitionTo(to));
    }

    [Fact]
    public void TransitionTo_same_status_is_a_no_op()
    {
        var product = CreateProduct();
        product.TransitionTo(ProductStatus.Active);
        product.ClearDomainEvents();

        product.TransitionTo(ProductStatus.Active);

        Assert.Equal(ProductStatus.Active, product.Status);
        Assert.Empty(product.DomainEvents);
    }

    [Fact]
    public void TransitionTo_draft_to_inactive_directly_throws()
    {
        var product = CreateProduct();

        Assert.Throws<InvalidProductStatusTransitionException>(() => product.TransitionTo(ProductStatus.Inactive));
    }

    [Fact]
    public void TransitionTo_raises_ProductStatusChangedDomainEvent()
    {
        var product = CreateProduct();
        product.ClearDomainEvents();

        product.TransitionTo(ProductStatus.Active);

        var domainEvent = Assert.Single(product.DomainEvents);
        var statusChanged = Assert.IsType<ProductStatusChangedDomainEvent>(domainEvent);
        Assert.Equal(ProductStatus.Draft, statusChanged.PreviousStatus);
        Assert.Equal(ProductStatus.Active, statusChanged.NewStatus);
    }
}
