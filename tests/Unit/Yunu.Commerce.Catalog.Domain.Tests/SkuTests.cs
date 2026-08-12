using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Products.Skus;
using Yunu.Commerce.Catalog.Domain.Products.Skus.Events;
using Xunit;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class SkuTests
{
    private static Sku CreateSku(string code = "256GB-BLACK")
    {
        return Sku.Create(
            SkuId.New(),
            ProductId.New(),
            new SkuCode(code));
    }

    [Fact]
    public void Create_Should_Default_To_Draft_Status()
    {
        var sku = CreateSku();

        Assert.Equal(SkuStatus.Draft, sku.Status);
    }

    [Fact]
    public void Create_Should_Require_A_Valid_ProductId()
    {
        var productId = ProductId.New();
        var sku = Sku.Create(SkuId.New(), productId, new SkuCode("256GB-BLACK"));

        Assert.Equal(productId, sku.ProductId);
    }

    [Fact]
    public void Create_Should_Raise_SkuCreatedDomainEvent()
    {
        var sku = CreateSku();

        var domainEvent = Assert.Single(sku.DomainEvents);
        var createdEvent = Assert.IsType<SkuCreatedDomainEvent>(domainEvent);
        Assert.Equal(sku.Id, createdEvent.SkuId);
        Assert.Equal(sku.ProductId, createdEvent.ProductId);
    }

    [Fact]
    public void Activate_Should_Transition_Status_And_Raise_Event()
    {
        var sku = CreateSku();
        sku.ClearDomainEvents();

        sku.Activate();

        Assert.Equal(SkuStatus.Active, sku.Status);

        var domainEvent = Assert.Single(sku.DomainEvents);
        Assert.IsType<SkuActivatedDomainEvent>(domainEvent);
    }

    [Fact]
    public void Activate_When_Already_Active_Should_Not_Raise_Event()
    {
        var sku = CreateSku();
        sku.Activate();
        sku.ClearDomainEvents();

        sku.Activate();

        Assert.Empty(sku.DomainEvents);
    }

    [Fact]
    public void Block_Should_Transition_Status_And_Raise_Event()
    {
        var sku = CreateSku();
        sku.ClearDomainEvents();

        sku.Block();

        Assert.Equal(SkuStatus.Inactive, sku.Status);

        var domainEvent = Assert.Single(sku.DomainEvents);
        Assert.IsType<SkuBlockedDomainEvent>(domainEvent);
    }

    [Fact]
    public void Discontinue_Should_Transition_Status_And_Raise_Event()
    {
        var sku = CreateSku();
        sku.ClearDomainEvents();

        sku.Discontinue();

        Assert.Equal(SkuStatus.Archived, sku.Status);

        var domainEvent = Assert.Single(sku.DomainEvents);
        Assert.IsType<SkuDiscontinuedDomainEvent>(domainEvent);
    }

    [Fact]
    public void ClearDomainEvents_Should_Empty_Collection()
    {
        var sku = CreateSku();

        Assert.NotEmpty(sku.DomainEvents);

        sku.ClearDomainEvents();

        Assert.Empty(sku.DomainEvents);
    }
}
