using Xunit;
using Yunu.Commerce.Catalog.Domain.Attributes;
using Yunu.Commerce.Catalog.Domain.Attributes.Events;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;
using Yunu.Commerce.Catalog.Domain.Skus.Events;

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
        sku.Activate();
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
    public void Activate_After_Discontinue_Should_Throw()
    {
        var sku = CreateSku();
        sku.Discontinue();

        Assert.Throws<InvalidSkuStatusTransitionException>(() => sku.Activate());
    }

    [Fact]
    public void Block_After_Discontinue_Should_Throw()
    {
        var sku = CreateSku();
        sku.Discontinue();

        Assert.Throws<InvalidSkuStatusTransitionException>(() => sku.Block());
    }

    [Fact]
    public void Discontinue_When_Already_Archived_Should_Not_Raise_Event()
    {
        var sku = CreateSku();
        sku.Discontinue();
        sku.ClearDomainEvents();

        sku.Discontinue();

        Assert.Empty(sku.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_Should_Empty_Collection()
    {
        var sku = CreateSku();

        Assert.NotEmpty(sku.DomainEvents);

        sku.ClearDomainEvents();

        Assert.Empty(sku.DomainEvents);
    }

    [Fact]
    public void Create_Without_Attributes_Should_Have_Empty_Attributes_Collection()
    {
        var sku = CreateSku();

        Assert.Empty(sku.Attributes);
    }

    [Fact]
    public void AssignAttribute_Should_Add_Attribute_And_Raise_Event()
    {
        var sku = CreateSku();
        sku.ClearDomainEvents();

        sku.AssignAttribute(new AttributeDefinitionId(14), "color", 1, SkuAttributeValue.ForText("Branco"));

        var attribute = Assert.Single(sku.Attributes);
        Assert.Equal("color", attribute.AttributeCode);

        var domainEvent = Assert.Single(sku.DomainEvents);
        Assert.IsType<SkuAttributeAssignedDomainEvent>(domainEvent);
    }

    [Fact]
    public void AssignAttribute_With_Enum_Value_And_Resolved_Option_Should_Succeed()
    {
        var sku = CreateSku();

        sku.AssignAttribute(new AttributeDefinitionId(47), "gender", 1, SkuAttributeValue.ForEnum("MALE"), new AttributeOptionId(1401));

        var attribute = Assert.Single(sku.Attributes);
        Assert.Equal(new AttributeOptionId(1401), attribute.AttributeOptionId);
    }

    [Fact]
    public void AssignAttribute_With_Money_Value_Should_Succeed()
    {
        var sku = CreateSku();

        sku.AssignAttribute(new AttributeDefinitionId(26), "price", 1, SkuAttributeValue.ForMoney(199.90m, "BRL"));

        var attribute = Assert.Single(sku.Attributes);
        Assert.Equal(199.90m, attribute.Value.MoneyAmount);
    }

    [Fact]
    public void AssignAttribute_With_Measurement_Value_Should_Succeed()
    {
        var sku = CreateSku();

        sku.AssignAttribute(new AttributeDefinitionId(15), "size", 1, SkuAttributeValue.ForMeasurement(41m, "BR"));

        var attribute = Assert.Single(sku.Attributes);
        Assert.Equal(41m, attribute.Value.MeasurementValue);
    }

    [Fact]
    public void AssignAttribute_Twice_With_Duplicate_DefinitionId_And_Sequence_And_Different_Value_Should_Throw()
    {
        var sku = CreateSku();
        sku.AssignAttribute(new AttributeDefinitionId(14), "color", 1, SkuAttributeValue.ForText("Branco"));

        Assert.Throws<InvalidOperationException>(() =>
            sku.AssignAttribute(new AttributeDefinitionId(14), "color", 1, SkuAttributeValue.ForText("Preto")));
    }

    [Fact]
    public void AssignAttribute_Twice_With_Same_Effective_Value_Should_Be_Idempotent_And_Not_Raise_Event()
    {
        var sku = CreateSku();
        sku.AssignAttribute(new AttributeDefinitionId(14), "color", 1, SkuAttributeValue.ForText("Branco"));
        sku.ClearDomainEvents();

        sku.AssignAttribute(new AttributeDefinitionId(14), "color", 1, SkuAttributeValue.ForText("Branco"));

        Assert.Single(sku.Attributes);
        Assert.Empty(sku.DomainEvents);
    }

    [Fact]
    public void ReplaceAttribute_Should_Change_Value_And_Raise_Event()
    {
        var sku = CreateSku();
        sku.AssignAttribute(new AttributeDefinitionId(14), "color", 1, SkuAttributeValue.ForText("Branco"));
        sku.ClearDomainEvents();

        sku.ReplaceAttribute(new AttributeDefinitionId(14), "color", 1, SkuAttributeValue.ForText("Preto"));

        var attribute = Assert.Single(sku.Attributes);
        Assert.Equal("Preto", attribute.Value.Text);

        var domainEvent = Assert.Single(sku.DomainEvents);
        Assert.IsType<SkuAttributeReplacedDomainEvent>(domainEvent);
    }

    [Fact]
    public void ReplaceAttribute_For_NonExistent_Assignment_Should_Throw()
    {
        var sku = CreateSku();

        Assert.Throws<InvalidOperationException>(() =>
            sku.ReplaceAttribute(new AttributeDefinitionId(14), "color", 1, SkuAttributeValue.ForText("Preto")));
    }

    [Fact]
    public void RemoveAttribute_Should_Remove_Assignment_And_Raise_Event()
    {
        var sku = CreateSku();
        sku.AssignAttribute(new AttributeDefinitionId(14), "color", 1, SkuAttributeValue.ForText("Branco"));
        sku.ClearDomainEvents();

        sku.RemoveAttribute(new AttributeDefinitionId(14), 1);

        Assert.Empty(sku.Attributes);

        var domainEvent = Assert.Single(sku.DomainEvents);
        Assert.IsType<SkuAttributeRemovedDomainEvent>(domainEvent);
    }

    [Fact]
    public void Attributes_Collection_Should_Be_ReadOnly()
    {
        var sku = CreateSku();
        sku.AssignAttribute(new AttributeDefinitionId(14), "color", 1, SkuAttributeValue.ForText("Branco"));

        Assert.IsAssignableFrom<IReadOnlyCollection<SkuAttribute>>(sku.Attributes);
    }
}
