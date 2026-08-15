using Xunit;
using Yunu.Commerce.Catalog.Application.AttributeCatalog;
using Yunu.Commerce.Catalog.Application.Skus.CreateSku;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Families;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Application.Tests;

public class CreateSkuHandlerTests
{
    private static GoogleCategoryReference CreateGoogleCategory()
    {
        return new GoogleCategoryReference(1234, "Apparel & Accessories > Shoes > Athletic Shoes");
    }

    private static Product CreateAndPersistProduct(FakeProductRepository productRepository)
    {
        var product = Product.Create(
            ProductId.New(),
            new ProductName("Apple iPhone 17 Pro"),
            description: null,
            new BrandId(Guid.NewGuid()),
            new FamilyId(Guid.NewGuid()),
            CreateGoogleCategory());

        productRepository.AddAsync(product, CancellationToken.None).GetAwaiter().GetResult();

        return product;
    }

    [Fact]
    public async Task Handle_With_Valid_ProductId_Should_Create_Sku()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var attributeCatalogRepository = new FakeAttributeCatalogRepository();

        var product = CreateAndPersistProduct(productRepository);

        var handler = new CreateSkuHandler(productRepository, skuRepository, attributeCatalogRepository);

        var command = new CreateSkuCommand
        {
            ProductId = product.Id.Value,
            Code = "256GB-BLACK",
            Gtin = "1234567890123"
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.SkuId);
        Assert.Equal(1, skuRepository.AddAsyncCallCount);
        Assert.Empty(result.Attributes);
    }

    [Fact]
    public async Task Handle_With_NonExistent_ProductId_Should_Throw()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var attributeCatalogRepository = new FakeAttributeCatalogRepository();
        var handler = new CreateSkuHandler(productRepository, skuRepository, attributeCatalogRepository);

        var command = new CreateSkuCommand
        {
            ProductId = Guid.NewGuid(),
            Code = "256GB-BLACK",
            Gtin = null
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(command, CancellationToken.None));

        Assert.Contains("does not exist", exception.Message);
        Assert.Equal(0, skuRepository.AddAsyncCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_With_Invalid_Code_Should_Throw(string? invalidCode)
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var attributeCatalogRepository = new FakeAttributeCatalogRepository();

        var product = CreateAndPersistProduct(productRepository);

        var handler = new CreateSkuHandler(productRepository, skuRepository, attributeCatalogRepository);

        var command = new CreateSkuCommand
        {
            ProductId = product.Id.Value,
            Code = invalidCode!,
            Gtin = null
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_With_Valid_Text_Attribute_Should_Assign_It()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var attributeCatalogRepository = new FakeAttributeCatalogRepository();

        attributeCatalogRepository.AddDefinition(new AttributeDefinitionResponse
        {
            AttributeDefinitionId = 14,
            Code = "color",
            Name = "Cor",
            DataType = "Text",
            Cardinality = "Single",
            IsVariantAxis = true,
            IsSearchable = true,
            IsFilterable = true,
            IsActive = true
        });

        var product = CreateAndPersistProduct(productRepository);

        var handler = new CreateSkuHandler(productRepository, skuRepository, attributeCatalogRepository);

        var command = new CreateSkuCommand
        {
            ProductId = product.Id.Value,
            Code = "WHITE-41",
            Attributes = new[]
            {
                new SkuAttributeInput { Code = "color", Value = "Branco" }
            }
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        var attribute = Assert.Single(result.Attributes);
        Assert.Equal(14, attribute.AttributeDefinitionId);
        Assert.Equal("color", attribute.AttributeCode);
        Assert.Equal("Branco", attribute.NormalizedValue);
    }

    [Fact]
    public async Task Handle_With_Valid_Enum_Attribute_Should_Resolve_Option_And_Assign_It()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var attributeCatalogRepository = new FakeAttributeCatalogRepository();

        attributeCatalogRepository.AddDefinition(new AttributeDefinitionResponse
        {
            AttributeDefinitionId = 47,
            Code = "gender",
            Name = "Gênero",
            DataType = "Enum",
            Cardinality = "Single",
            IsVariantAxis = false,
            IsSearchable = true,
            IsFilterable = true,
            IsActive = true
        });

        attributeCatalogRepository.AddOption(new AttributeOptionResponse
        {
            AttributeOptionId = 1401,
            AttributeDefinitionId = 47,
            Code = "MALE",
            Name = "Masculino",
            IsActive = true
        });

        var product = CreateAndPersistProduct(productRepository);

        var handler = new CreateSkuHandler(productRepository, skuRepository, attributeCatalogRepository);

        var command = new CreateSkuCommand
        {
            ProductId = product.Id.Value,
            Code = "RUNNER-MALE",
            Attributes = new[]
            {
                new SkuAttributeInput { Code = "gender", OptionCode = "MALE" }
            }
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        var attribute = Assert.Single(result.Attributes);
        Assert.Equal(1401, attribute.AttributeOptionId);
    }

    [Fact]
    public async Task Handle_With_Unknown_Attribute_Code_Should_Throw()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var attributeCatalogRepository = new FakeAttributeCatalogRepository();

        var product = CreateAndPersistProduct(productRepository);

        var handler = new CreateSkuHandler(productRepository, skuRepository, attributeCatalogRepository);

        var command = new CreateSkuCommand
        {
            ProductId = product.Id.Value,
            Code = "RUNNER-41",
            Attributes = new[]
            {
                new SkuAttributeInput { Code = "unknown_attribute", Value = "x" }
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_With_Inactive_Attribute_Definition_Should_Throw()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var attributeCatalogRepository = new FakeAttributeCatalogRepository();

        attributeCatalogRepository.AddDefinition(new AttributeDefinitionResponse
        {
            AttributeDefinitionId = 99,
            Code = "deprecated_attribute",
            Name = "Deprecated",
            DataType = "Text",
            Cardinality = "Single",
            IsVariantAxis = false,
            IsSearchable = false,
            IsFilterable = false,
            IsActive = false
        });

        var product = CreateAndPersistProduct(productRepository);

        var handler = new CreateSkuHandler(productRepository, skuRepository, attributeCatalogRepository);

        var command = new CreateSkuCommand
        {
            ProductId = product.Id.Value,
            Code = "RUNNER-41",
            Attributes = new[]
            {
                new SkuAttributeInput { Code = "deprecated_attribute", Value = "x" }
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_With_Numeric_Value_Outside_Range_Should_Throw()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var attributeCatalogRepository = new FakeAttributeCatalogRepository();

        attributeCatalogRepository.AddDefinition(new AttributeDefinitionResponse
        {
            AttributeDefinitionId = 60,
            Code = "popularity_rank",
            Name = "Popularidade",
            DataType = "Decimal",
            Cardinality = "Single",
            MinNumericValue = 0,
            MaxNumericValue = 100,
            IsVariantAxis = false,
            IsSearchable = true,
            IsFilterable = true,
            IsActive = true
        });

        var product = CreateAndPersistProduct(productRepository);

        var handler = new CreateSkuHandler(productRepository, skuRepository, attributeCatalogRepository);

        var command = new CreateSkuCommand
        {
            ProductId = product.Id.Value,
            Code = "RUNNER-41",
            Attributes = new[]
            {
                new SkuAttributeInput { Code = "popularity_rank", Value = "150" }
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }
}

