using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;
using Yunu.Commerce.Catalog.Domain.Attributes;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;
using Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Integration tests for MongoSkuRepository against a real MongoDB instance via
/// Testcontainers (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// Covers AddAsync/GetByIdAsync/GetByProductIdAsync, matching the ISkuRepository
/// contract. Skus are persisted in their own "skus" collection, independent from
/// "products".
/// </summary>
public sealed class MongoSkuRepositoryTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder("mongo:8.0").Build();
    private IMongoClient _mongoClient = null!;
    private MongoSkuRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();

        _mongoClient = new MongoClient(_mongoContainer.GetConnectionString());

        var options = Options.Create(new CatalogMongoOptions
        {
            ConnectionString = _mongoContainer.GetConnectionString(),
            DatabaseName = "yunu_catalog_tests"
        });

        _repository = new MongoSkuRepository(_mongoClient, options);
    }

    public async Task DisposeAsync()
    {
        await _mongoContainer.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_Then_GetByIdAsync_Should_RoundTrip_Sku()
    {
        var sku = Sku.Create(SkuId.New(), ProductId.New(), new SkuCode("256GB-BLACK"), "0000000000001");

        await _repository.AddAsync(sku, CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(sku.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(sku.Id, retrieved!.Id);
        Assert.Equal(sku.ProductId, retrieved.ProductId);
        Assert.Equal(sku.Code, retrieved.Code);
        Assert.Equal(sku.Gtin, retrieved.Gtin);
        Assert.Equal(sku.Status, retrieved.Status);
    }

    [Fact]
    public async Task GetByIdAsync_For_NonExistent_Sku_Should_Return_Null()
    {
        var retrieved = await _repository.GetByIdAsync(SkuId.New(), CancellationToken.None);

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetByProductIdAsync_Should_Return_Only_Skus_For_Requested_Product()
    {
        var productId = ProductId.New();
        var otherProductId = ProductId.New();

        var sku1 = Sku.Create(SkuId.New(), productId, new SkuCode("256GB-BLACK"));
        var sku2 = Sku.Create(SkuId.New(), productId, new SkuCode("512GB-BLACK"));
        var otherSku = Sku.Create(SkuId.New(), otherProductId, new SkuCode("OTHER-SKU"));

        await _repository.AddAsync(sku1, CancellationToken.None);
        await _repository.AddAsync(sku2, CancellationToken.None);
        await _repository.AddAsync(otherSku, CancellationToken.None);

        var retrieved = await _repository.GetByProductIdAsync(productId, CancellationToken.None);

        Assert.Equal(2, retrieved.Count);
        Assert.All(retrieved, sku => Assert.Equal(productId, sku.ProductId));
    }

    [Fact]
    public async Task SkuStatus_Should_RoundTrip_As_Correct_Enum_Value()
    {
        var sku = Sku.Create(SkuId.New(), ProductId.New(), new SkuCode("STATUS-TEST"), status: SkuStatus.Active);

        await _repository.AddAsync(sku, CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(sku.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(SkuStatus.Active, retrieved!.Status);
    }

    [Fact]
    public async Task AddAsync_Then_GetByIdAsync_Should_RoundTrip_Every_Supported_Attribute_DataType()
    {
        var sku = Sku.Create(SkuId.New(), ProductId.New(), new SkuCode("ATTR-ROUNDTRIP"));

        sku.AssignAttribute(new AttributeDefinitionId(14), "color", 1, SkuAttributeValue.ForText("Branco"));
        sku.AssignAttribute(new AttributeDefinitionId(30), "multipack", 1, SkuAttributeValue.ForInteger(3));
        sku.AssignAttribute(new AttributeDefinitionId(60), "popularity_rank", 1, SkuAttributeValue.ForDecimal(87.5m));
        sku.AssignAttribute(new AttributeDefinitionId(31), "is_bundle", 1, SkuAttributeValue.ForBoolean(true));
        sku.AssignAttribute(new AttributeDefinitionId(25), "availability_date", 1, SkuAttributeValue.ForDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        sku.AssignAttribute(new AttributeDefinitionId(26), "price", 1, SkuAttributeValue.ForMoney(199.90m, "BRL"));
        sku.AssignAttribute(new AttributeDefinitionId(15), "size", 1, SkuAttributeValue.ForMeasurement(41m, "BR"));
        sku.AssignAttribute(new AttributeDefinitionId(8), "link", 1, SkuAttributeValue.ForUrl("https://example.com/product"));
        sku.AssignAttribute(new AttributeDefinitionId(47), "gender", 1, SkuAttributeValue.ForEnum("MALE"), new AttributeOptionId(1401));
        sku.AssignAttribute(new AttributeDefinitionId(13), "product_detail", 1, SkuAttributeValue.ForJson("{\"section\":\"General\"}"));

        await _repository.AddAsync(sku, CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(sku.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(10, retrieved!.Attributes.Count);

        var color = retrieved.Attributes.Single(a => a.AttributeCode == "color");
        Assert.Equal("Branco", color.Value.Text);

        var multipack = retrieved.Attributes.Single(a => a.AttributeCode == "multipack");
        Assert.Equal(3, multipack.Value.Integer);

        var popularity = retrieved.Attributes.Single(a => a.AttributeCode == "popularity_rank");
        Assert.Equal(87.5m, popularity.Value.Decimal);

        var isBundle = retrieved.Attributes.Single(a => a.AttributeCode == "is_bundle");
        Assert.True(isBundle.Value.Boolean);

        var availabilityDate = retrieved.Attributes.Single(a => a.AttributeCode == "availability_date");
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), availabilityDate.Value.DateTimeValue);

        var price = retrieved.Attributes.Single(a => a.AttributeCode == "price");
        Assert.Equal(199.90m, price.Value.MoneyAmount);
        Assert.Equal("BRL", price.Value.CurrencyCode);

        var size = retrieved.Attributes.Single(a => a.AttributeCode == "size");
        Assert.Equal(41m, size.Value.MeasurementValue);
        Assert.Equal("BR", size.Value.UnitCode);

        var link = retrieved.Attributes.Single(a => a.AttributeCode == "link");
        Assert.Equal("https://example.com/product", link.Value.Url);

        var gender = retrieved.Attributes.Single(a => a.AttributeCode == "gender");
        Assert.Equal(new AttributeOptionId(1401), gender.AttributeOptionId);

        var productDetail = retrieved.Attributes.Single(a => a.AttributeCode == "product_detail");
        Assert.Equal("{\"section\":\"General\"}", productDetail.Value.Json);
    }

    [Fact]
    public async Task GetByIdAsync_For_Legacy_Document_Without_Attributes_Field_Should_Hydrate_With_Empty_Attributes()
    {
        var database = _mongoClient.GetDatabase("yunu_catalog_tests");
        var collection = database.GetCollection<BsonDocument>("skus");

        var legacySkuId = Guid.NewGuid();

        var legacyDocument = new BsonDocument
        {
            { "_id", legacySkuId.ToString() },
            { "ProductId", Guid.NewGuid().ToString() },
            { "Code", "LEGACY-SKU" },
            { "Gtin", BsonNull.Value },
            { "Status", nameof(SkuStatus.Draft) }
        };

        await collection.InsertOneAsync(legacyDocument);

        var retrieved = await _repository.GetByIdAsync(new SkuId(legacySkuId), CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Empty(retrieved!.Attributes);
    }

    [Fact]
    public async Task AddAsync_Should_Omit_Null_Typed_Attribute_Properties_From_Bson()
    {
        var sku = Sku.Create(SkuId.New(), ProductId.New(), new SkuCode("ATTR-BSON-SHAPE"));

        sku.AssignAttribute(new AttributeDefinitionId(14), "color", 1, SkuAttributeValue.ForText("Branco"));
        sku.AssignAttribute(new AttributeDefinitionId(47), "gender", 1, SkuAttributeValue.ForEnum("MALE"), new AttributeOptionId(1401));

        await _repository.AddAsync(sku, CancellationToken.None);

        var database = _mongoClient.GetDatabase("yunu_catalog_tests");
        var collection = database.GetCollection<BsonDocument>("skus");
        var rawDocument = await collection.Find(new BsonDocument("_id", sku.Id.Value.ToString())).SingleAsync();
        var rawAttributes = rawDocument["Attributes"].AsBsonArray;

        var textAttribute = rawAttributes.Select(a => a.AsBsonDocument).Single(a => a["AttributeCode"].AsString == "color");
        Assert.True(textAttribute.Contains("Text"));
        Assert.False(textAttribute.Contains("Integer"));
        Assert.False(textAttribute.Contains("Decimal"));
        Assert.False(textAttribute.Contains("Boolean"));
        Assert.False(textAttribute.Contains("DateTimeValue"));
        Assert.False(textAttribute.Contains("MoneyAmount"));
        Assert.False(textAttribute.Contains("CurrencyCode"));
        Assert.False(textAttribute.Contains("MeasurementValue"));
        Assert.False(textAttribute.Contains("UnitCode"));
        Assert.False(textAttribute.Contains("Url"));
        Assert.False(textAttribute.Contains("EnumOptionCode"));
        Assert.False(textAttribute.Contains("Json"));
        Assert.False(textAttribute.Contains("AttributeOptionId"));
        Assert.False(textAttribute.Contains("Confidence"));

        var enumAttribute = rawAttributes.Select(a => a.AsBsonDocument).Single(a => a["AttributeCode"].AsString == "gender");
        Assert.True(enumAttribute.Contains("EnumOptionCode"));
        Assert.True(enumAttribute.Contains("AttributeOptionId"));
        Assert.False(enumAttribute.Contains("Text"));
        Assert.False(enumAttribute.Contains("Integer"));
        Assert.False(enumAttribute.Contains("Decimal"));
        Assert.False(enumAttribute.Contains("Boolean"));
        Assert.False(enumAttribute.Contains("DateTimeValue"));
        Assert.False(enumAttribute.Contains("MoneyAmount"));
        Assert.False(enumAttribute.Contains("CurrencyCode"));
        Assert.False(enumAttribute.Contains("MeasurementValue"));
        Assert.False(enumAttribute.Contains("UnitCode"));
        Assert.False(enumAttribute.Contains("Url"));
        Assert.False(enumAttribute.Contains("Json"));
        Assert.False(enumAttribute.Contains("Confidence"));
    }

    [Fact]
    public async Task GetByIdAsync_For_Document_With_Explicit_Null_Attribute_Fields_Should_Hydrate_Correctly()
    {
        var database = _mongoClient.GetDatabase("yunu_catalog_tests");
        var collection = database.GetCollection<BsonDocument>("skus");

        var skuId = Guid.NewGuid();

        var attributeDocument = new BsonDocument
        {
            { "AttributeDefinitionId", 14 },
            { "AttributeCode", "color" },
            { "Sequence", 1 },
            { "DataType", nameof(SkuAttributeDataType.Text) },
            { "RawValue", "Branco" },
            { "NormalizedValue", "Branco" },
            { "Text", "Branco" },
            { "Integer", BsonNull.Value },
            { "Decimal", BsonNull.Value },
            { "Boolean", BsonNull.Value },
            { "DateTimeValue", BsonNull.Value },
            { "MoneyAmount", BsonNull.Value },
            { "CurrencyCode", BsonNull.Value },
            { "MeasurementValue", BsonNull.Value },
            { "UnitCode", BsonNull.Value },
            { "Url", BsonNull.Value },
            { "EnumOptionCode", BsonNull.Value },
            { "Json", BsonNull.Value },
            { "AttributeOptionId", BsonNull.Value },
            { "Source", nameof(SkuAttributeSource.User) },
            { "Confidence", BsonNull.Value }
        };

        var document = new BsonDocument
        {
            { "_id", skuId.ToString() },
            { "ProductId", Guid.NewGuid().ToString() },
            { "Code", "EXPLICIT-NULLS" },
            { "Gtin", BsonNull.Value },
            { "Status", nameof(SkuStatus.Draft) },
            { "Attributes", new BsonArray { attributeDocument } }
        };

        await collection.InsertOneAsync(document);

        var retrieved = await _repository.GetByIdAsync(new SkuId(skuId), CancellationToken.None);

        Assert.NotNull(retrieved);
        var attribute = Assert.Single(retrieved!.Attributes);
        Assert.Equal("Branco", attribute.Value.Text);
        Assert.Null(attribute.AttributeOptionId);
        Assert.Null(attribute.Confidence);
    }

    [Fact]
    public async Task GetByIdAsync_For_Document_With_Missing_Optional_Attribute_Fields_Should_Hydrate_Correctly()
    {
        var database = _mongoClient.GetDatabase("yunu_catalog_tests");
        var collection = database.GetCollection<BsonDocument>("skus");

        var skuId = Guid.NewGuid();

        var attributeDocument = new BsonDocument
        {
            { "AttributeDefinitionId", 47 },
            { "AttributeCode", "gender" },
            { "Sequence", 1 },
            { "DataType", nameof(SkuAttributeDataType.Enum) },
            { "RawValue", "MALE" },
            { "NormalizedValue", "MALE" },
            { "EnumOptionCode", "MALE" },
            { "AttributeOptionId", 1401 },
            { "Source", nameof(SkuAttributeSource.User) }
        };

        var document = new BsonDocument
        {
            { "_id", skuId.ToString() },
            { "ProductId", Guid.NewGuid().ToString() },
            { "Code", "MISSING-OPTIONAL-FIELDS" },
            { "Gtin", BsonNull.Value },
            { "Status", nameof(SkuStatus.Draft) },
            { "Attributes", new BsonArray { attributeDocument } }
        };

        await collection.InsertOneAsync(document);

        var retrieved = await _repository.GetByIdAsync(new SkuId(skuId), CancellationToken.None);

        Assert.NotNull(retrieved);
        var attribute = Assert.Single(retrieved!.Attributes);
        Assert.Equal("MALE", attribute.Value.EnumOptionCode);
        Assert.Equal(new AttributeOptionId(1401), attribute.AttributeOptionId);
        Assert.Null(attribute.Confidence);
    }

    [Fact]
    public async Task AddAsync_Then_GetByIdAsync_Should_RoundTrip_Text_And_Enum_Attributes()
    {
        var sku = Sku.Create(SkuId.New(), ProductId.New(), new SkuCode("TEXT-ENUM-ROUNDTRIP"));

        sku.AssignAttribute(new AttributeDefinitionId(14), "color", 1, SkuAttributeValue.ForText("Branco"));
        sku.AssignAttribute(new AttributeDefinitionId(47), "gender", 1, SkuAttributeValue.ForEnum("MALE"), new AttributeOptionId(1401));

        await _repository.AddAsync(sku, CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(sku.Id, CancellationToken.None);

        Assert.NotNull(retrieved);

        var color = retrieved!.Attributes.Single(a => a.AttributeCode == "color");
        Assert.Equal(SkuAttributeDataType.Text, color.DataType);
        Assert.Equal("Branco", color.Value.Text);
        Assert.Null(color.AttributeOptionId);

        var gender = retrieved.Attributes.Single(a => a.AttributeCode == "gender");
        Assert.Equal(SkuAttributeDataType.Enum, gender.DataType);
        Assert.Equal("MALE", gender.Value.EnumOptionCode);
        Assert.Equal(new AttributeOptionId(1401), gender.AttributeOptionId);
    }
}
