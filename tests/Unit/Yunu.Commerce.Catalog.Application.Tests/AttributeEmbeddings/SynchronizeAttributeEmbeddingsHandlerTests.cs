using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Yunu.Commerce.AI.Application.Embeddings;
using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

namespace Yunu.Commerce.Catalog.Application.Tests.AttributeEmbeddings;

/// <summary>
/// Unit tests for SynchronizeAttributeEmbeddingsHandler (docs task: "SKU
/// attribute embedding synchronization pipeline").
/// </summary>
public sealed class SynchronizeAttributeEmbeddingsHandlerTests
{
    private static AttributeDefinitionSource CreateDefinition(string code = "color", DateTime? updatedAt = null) => new()
    {
        AttributeDefinitionId = 14,
        Code = code,
        GoogleAttributeName = code,
        Name = "Cor",
        Description = "Cor principal ou combinação de cores do SKU.",
        SemanticText = "cor, tonalidade, color",
        DataType = "Text",
        Cardinality = "Single",
        UnitFamily = null,
        IsGoogleMerchantAttribute = true,
        IsVariantAxis = true,
        IsSearchable = true,
        IsFilterable = true,
        IsRequiredByDefault = false,
        DisplayOrder = 10,
        IsActive = true,
        UpdatedAt = updatedAt ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    /// <summary>
    /// A non-searchable-but-active definition, equivalent to the real-world
    /// "shipping_weight" regression case: IsActive = true, IsSearchable =
    /// false. Must still be read and synchronized, since IsSearchable only
    /// gates storefront search, not AI embedding generation. Identified by
    /// Code, never by a fixed numeric id.
    /// </summary>
    private static AttributeDefinitionSource CreateNonSearchableDefinition(string code = "shipping_weight") => new()
    {
        AttributeDefinitionId = 37,
        Code = code,
        GoogleAttributeName = code,
        Name = "Peso para frete",
        Description = "Peso usado no cálculo de entrega.",
        SemanticText = "peso embalagem frete transporte cálculo entrega",
        DataType = "Measurement",
        Cardinality = "Single",
        UnitFamily = "Weight",
        IsGoogleMerchantAttribute = true,
        IsVariantAxis = false,
        IsSearchable = false,
        IsFilterable = false,
        IsRequiredByDefault = false,
        DisplayOrder = 40,
        IsActive = true,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static AttributeOptionSource CreateOption() => new()
    {
        AttributeOptionId = 1401,
        AttributeDefinitionId = 47,
        AttributeCode = "gender",
        AttributeName = "Gênero",
        OptionCode = "MALE",
        GoogleValue = "male",
        OptionName = "Masculino",
        OptionSemanticText = "produto para homem masculino",
        DisplayOrder = 10,
        IsActive = true
    };

    private static (SynchronizeAttributeEmbeddingsHandler Handler, FakeAttributeEmbeddingSourceRepository Source, FakeAttributeEmbeddingRepository Repository, FakeEmbeddingProvider Provider, FakeAttributeEmbeddingSynchronizationGuard Guard) CreateSut()
    {
        var source = new FakeAttributeEmbeddingSourceRepository();
        var repository = new FakeAttributeEmbeddingRepository();
        var provider = new FakeEmbeddingProvider();
        var guard = new FakeAttributeEmbeddingSynchronizationGuard();

        var orchestrator = new EmbeddingOrchestrator(
            new[] { provider },
            Options.Create(new EmbeddingOptions { DefaultProvider = FakeEmbeddingProvider.ProviderName }));

        var syncOptions = Options.Create(new AttributeEmbeddingsSyncOptions
        {
            BatchSize = 50,
            MaxDegreeOfParallelism = 1,
            Locale = "pt-BR"
        });

        var handler = new SynchronizeAttributeEmbeddingsHandler(
            source,
            repository,
            orchestrator,
            guard,
            Options.Create(new EmbeddingOptions { DefaultProvider = FakeEmbeddingProvider.ProviderName }),
            syncOptions,
            NullLogger<SynchronizeAttributeEmbeddingsHandler>.Instance);

        return (handler, source, repository, provider, guard);
    }

    [Fact]
    public async Task HandleAsync_Should_Generate_Missing_Definition_Embedding()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        source.AddDefinition(CreateDefinition());

        var result = await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.DefinitionsRead);
        Assert.Equal(1, result.Generated);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.Equal(1, provider.CallCount);

        var row = Assert.Single(repository.Rows.Values);
        Assert.Equal("AttributeDefinition", row.EntityType);
        Assert.Equal("color", row.EntityId);
        Assert.NotNull(row.Embedding);
        Assert.NotNull(row.EmbeddedAt);
        Assert.Equal(row.ContentHash, row.EmbeddedContentHash);
    }

    [Fact]
    public async Task HandleAsync_Should_Update_Existing_Seed_Row_Instead_Of_Inserting_Duplicate()
    {
        var (handler, source, repository, _, _) = CreateSut();
        var definition = CreateDefinition();
        source.AddDefinition(definition);

        var semanticText = AttributeSemanticDocumentBuilder.BuildDefinitionText(definition);
        var seedId = Guid.NewGuid();

        repository.Seed(new AttributeEmbeddingDocument
        {
            Id = seedId,
            EntityType = "AttributeDefinition",
            EntityId = "color",
            AttributeCode = "color",
            OptionCode = null,
            Locale = "pt-BR",
            Name = "Cor",
            SemanticText = semanticText,
            Embedding = null,
            EmbeddingModel = null,
            ContentHash = AttributeSemanticDocumentBuilder.ComputeContentHash(semanticText),
            EmbeddedContentHash = null,
            Metadata = "{}",
            IsActive = true
        });

        var result = await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(0, result.Generated);
        Assert.Equal(1, result.Updated);

        var row = Assert.Single(repository.Rows.Values);
        Assert.Equal(seedId, row.Id);
        Assert.NotNull(row.Embedding);
    }

    [Fact]
    public async Task HandleAsync_Should_Skip_Unchanged_Row_With_Valid_Embedding()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        var definition = CreateDefinition();
        source.AddDefinition(definition);

        var semanticText = AttributeSemanticDocumentBuilder.BuildDefinitionText(definition);
        var contentHash = AttributeSemanticDocumentBuilder.ComputeContentHash(semanticText);

        repository.Seed(new AttributeEmbeddingDocument
        {
            Id = Guid.NewGuid(),
            EntityType = "AttributeDefinition",
            EntityId = "color",
            AttributeCode = "color",
            OptionCode = null,
            Locale = "pt-BR",
            Name = "Cor",
            SemanticText = semanticText,
            Embedding = new float[1536],
            EmbeddingModel = "fake-model",
            ContentHash = contentHash,
            EmbeddedContentHash = contentHash,
            Metadata = "{}",
            EmbeddedAt = DateTime.UtcNow,
            IsActive = true
        });

        var result = await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(0, result.Generated);
        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task HandleAsync_Should_Regenerate_Row_When_Semantic_Text_Changes()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        var definition = CreateDefinition();
        source.AddDefinition(definition);

        repository.Seed(new AttributeEmbeddingDocument
        {
            Id = Guid.NewGuid(),
            EntityType = "AttributeDefinition",
            EntityId = "color",
            AttributeCode = "color",
            OptionCode = null,
            Locale = "pt-BR",
            Name = "Cor",
            SemanticText = "old text",
            Embedding = new float[1536],
            EmbeddingModel = "fake-model",
            ContentHash = AttributeSemanticDocumentBuilder.ComputeContentHash("old text"),
            EmbeddedContentHash = AttributeSemanticDocumentBuilder.ComputeContentHash("old text"),
            Metadata = "{}",
            EmbeddedAt = DateTime.UtcNow,
            IsActive = true
        });

        var result = await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task HandleAsync_Should_Regenerate_Row_When_Embedding_Is_Null_Even_If_Hash_Matches()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        var definition = CreateDefinition();
        source.AddDefinition(definition);

        var semanticText = AttributeSemanticDocumentBuilder.BuildDefinitionText(definition);
        var contentHash = AttributeSemanticDocumentBuilder.ComputeContentHash(semanticText);

        repository.Seed(new AttributeEmbeddingDocument
        {
            Id = Guid.NewGuid(),
            EntityType = "AttributeDefinition",
            EntityId = "color",
            AttributeCode = "color",
            OptionCode = null,
            Locale = "pt-BR",
            Name = "Cor",
            SemanticText = semanticText,
            Embedding = null,
            EmbeddingModel = null,
            ContentHash = contentHash,
            EmbeddedContentHash = contentHash,
            Metadata = "{}",
            IsActive = true
        });

        var result = await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task HandleAsync_Should_Synchronize_AttributeOptions()
    {
        var (handler, source, repository, _, _) = CreateSut();
        source.AddOption(CreateOption());

        var result = await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.OptionsRead);
        Assert.Equal(1, result.Generated);

        var row = Assert.Single(repository.Rows.Values);
        Assert.Equal("AttributeOption", row.EntityType);
        Assert.Equal("gender:MALE", row.EntityId);
        Assert.Equal("gender", row.AttributeCode);
        Assert.Equal("MALE", row.OptionCode);
    }

    [Fact]
    public async Task HandleAsync_Should_Use_Definition_Code_As_Definition_EntityId()
    {
        var (handler, source, repository, _, _) = CreateSut();
        source.AddDefinition(CreateDefinition("size"));

        await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        var row = Assert.Single(repository.Rows.Values);
        Assert.Equal("size", row.EntityId);
        Assert.Null(row.GoogleCategoryId);
        Assert.Null(row.SkuId);
    }

    [Fact]
    public async Task HandleAsync_Should_Use_Composite_EntityId_For_Options()
    {
        var (handler, source, repository, _, _) = CreateSut();
        source.AddOption(CreateOption());

        await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        var row = Assert.Single(repository.Rows.Values);
        Assert.Equal("gender:MALE", row.EntityId);
    }

    [Fact]
    public async Task HandleAsync_Should_Not_Synchronize_SkuAttributeValues()
    {
        var (handler, source, repository, _, _) = CreateSut();
        source.AddDefinition(CreateDefinition());
        source.AddOption(CreateOption());

        await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.All(repository.Rows.Values, row => Assert.NotEqual("SkuAttributeValue", row.EntityType));
    }

    [Fact]
    public async Task HandleAsync_Should_Prevent_Concurrent_Synchronization()
    {
        var (handler, _, _, _, guard) = CreateSut();
        guard.AlwaysBusy = true;

        await Assert.ThrowsAsync<AttributeEmbeddingSynchronizationInProgressException>(
            () => handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_Should_Report_Correct_Counts()
    {
        var (handler, source, repository, _, _) = CreateSut();
        source.AddDefinition(CreateDefinition("color"));
        source.AddDefinition(CreateDefinition("size"));
        source.AddOption(CreateOption());

        // Seed "size" as already up-to-date so it is skipped.
        var sizeDefinition = CreateDefinition("size");
        var sizeText = AttributeSemanticDocumentBuilder.BuildDefinitionText(sizeDefinition);
        var sizeHash = AttributeSemanticDocumentBuilder.ComputeContentHash(sizeText);

        repository.Seed(new AttributeEmbeddingDocument
        {
            Id = Guid.NewGuid(),
            EntityType = "AttributeDefinition",
            EntityId = "size",
            AttributeCode = "size",
            OptionCode = null,
            Locale = "pt-BR",
            Name = "Cor",
            SemanticText = sizeText,
            Embedding = new float[1536],
            EmbeddingModel = "fake-model",
            ContentHash = sizeHash,
            EmbeddedContentHash = sizeHash,
            Metadata = "{}",
            EmbeddedAt = DateTime.UtcNow,
            IsActive = true
        });

        var result = await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(2, result.DefinitionsRead);
        Assert.Equal(1, result.OptionsRead);
        Assert.Equal(2, result.Generated); // color definition + option
        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Skipped); // size definition
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task HandleAsync_Should_Not_Mark_EmbeddedContentHash_After_Azure_Failure()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        source.AddDefinition(CreateDefinition());
        provider.ThrowOnGenerate = true;

        var result = await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(0, result.Generated);
        Assert.Empty(repository.Rows);
    }

    [Fact]
    public async Task HandleAsync_Should_Synchronize_Active_Non_Searchable_Definitions()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        var shippingWeight = CreateNonSearchableDefinition();
        source.AddDefinition(shippingWeight);

        var result = await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.DefinitionsRead);
        Assert.Equal(1, result.Generated);
        Assert.Equal(0, result.Failed);
        Assert.Equal(1, provider.CallCount);

        var row = Assert.Single(repository.Rows.Values);
        Assert.Equal("AttributeDefinition", row.EntityType);
        Assert.Equal("shipping_weight", row.EntityId);
        Assert.NotNull(row.Embedding);
    }

    [Fact]
    public async Task HandleAsync_Should_Report_68_DefinitionsRead_For_45_Searchable_Plus_23_NonSearchable()
    {
        var (handler, source, _, _, _) = CreateSut();

        for (var i = 0; i < 45; i++)
        {
            source.AddDefinition(CreateDefinition($"searchable_{i}"));
        }

        for (var i = 0; i < 23; i++)
        {
            source.AddDefinition(CreateNonSearchableDefinition($"non_searchable_{i}"));
        }

        var result = await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(68, result.DefinitionsRead);
        Assert.Equal(68, result.Generated);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task HandleAsync_Should_Synchronize_Options_Of_Active_Non_Searchable_Definitions()
    {
        var (handler, source, repository, _, _) = CreateSut();
        source.AddOption(new AttributeOptionSource
        {
            AttributeOptionId = 5001,
            AttributeDefinitionId = 37,
            AttributeCode = "shipping_weight",
            AttributeName = "Peso para frete",
            OptionCode = "STANDARD",
            GoogleValue = null,
            OptionName = "Padrão",
            OptionSemanticText = "faixa de peso padrão para cálculo de frete",
            DisplayOrder = 10,
            IsActive = true
        });

        var result = await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.OptionsRead);
        Assert.Equal(1, result.Generated);

        var row = Assert.Single(repository.Rows.Values);
        Assert.Equal("AttributeOption", row.EntityType);
        Assert.Equal("shipping_weight", row.AttributeCode);
    }

    [Fact]
    public async Task HandleAsync_Should_Be_Idempotent_For_Previously_Pending_Non_Searchable_Definition()
    {
        // Regression scenario: a row pre-created by the static PostgreSQL seed
        // (embedding = null) for an active, non-searchable definition like
        // shipping_weight. First synchronization must generate the embedding;
        // a second synchronization without any source change must skip it.
        var (handler, source, repository, provider, _) = CreateSut();
        var shippingWeight = CreateNonSearchableDefinition();
        source.AddDefinition(shippingWeight);

        var semanticText = AttributeSemanticDocumentBuilder.BuildDefinitionText(shippingWeight);
        var seedId = Guid.NewGuid();

        repository.Seed(new AttributeEmbeddingDocument
        {
            Id = seedId,
            EntityType = "AttributeDefinition",
            EntityId = "shipping_weight",
            AttributeCode = "shipping_weight",
            OptionCode = null,
            Locale = "pt-BR",
            Name = "Peso para frete",
            SemanticText = semanticText,
            Embedding = null,
            EmbeddingModel = null,
            ContentHash = AttributeSemanticDocumentBuilder.ComputeContentHash(semanticText),
            EmbeddedContentHash = null,
            Metadata = "{}",
            IsActive = true
        });

        var firstResult = await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(0, firstResult.Generated);
        Assert.Equal(1, firstResult.Updated);
        Assert.Equal(1, provider.CallCount);

        var row = Assert.Single(repository.Rows.Values);
        Assert.Equal(seedId, row.Id);
        Assert.NotNull(row.Embedding);

        // Second synchronization: source unchanged, embedding already present
        // with a matching hash → must be skipped, not regenerated.
        var secondResult = await handler.HandleAsync(new SynchronizeAttributeEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(0, secondResult.Generated);
        Assert.Equal(0, secondResult.Updated);
        Assert.Equal(1, secondResult.Skipped);
        Assert.Equal(1, provider.CallCount); // no additional provider call
    }
}
