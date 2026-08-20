using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Yunu.Commerce.AI.Application.Embeddings;
using Yunu.Commerce.Catalog.Application.SegmentEmbeddings;
using Yunu.Commerce.Catalog.Application.Tests.AttributeEmbeddings;

namespace Yunu.Commerce.Catalog.Application.Tests.SegmentEmbeddings;

/// <summary>
/// Unit tests for SynchronizeSegmentEmbeddingsHandler (docs task:
/// "Implementar sincronização de embeddings de segmentos").
/// </summary>
public sealed class SynchronizeSegmentEmbeddingsHandlerTests
{
    private static SegmentDefinitionSource CreateDefinition(
        string code = "gender",
        string assignmentScope = "ProductWithSkuOverride",
        DateTime? updatedAt = null,
        long segmentDefinitionId = 14) => new()
    {
        SegmentDefinitionId = segmentDefinitionId,
        Code = code,
        Name = "Gênero",
        Description = "Público-alvo por gênero.",
        SemanticText = "masculino feminino unissex",
        SelectionMode = "Single",
        AssignmentScope = assignmentScope,
        UpdatedAt = updatedAt ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static SegmentOptionSource CreateOption(
        long segmentDefinitionId = 14,
        string assignmentScope = "ProductWithSkuOverride") => new()
    {
        SegmentOptionId = 1401,
        SegmentDefinitionId = segmentDefinitionId,
        SegmentCode = "gender",
        SegmentName = "Gênero",
        OptionCode = "MALE",
        OptionName = "Masculino",
        OptionDescription = "Produtos destinados ao público masculino.",
        OptionSemanticText = "homem masculino",
        AssignmentScope = assignmentScope,
        DisplayOrder = 10,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static (
        SynchronizeSegmentEmbeddingsHandler Handler,
        FakeSegmentEmbeddingSourceRepository Source,
        FakeSegmentEmbeddingRepository Repository,
        FakeEmbeddingProvider Provider,
        FakeSegmentEmbeddingSynchronizationGuard Guard) CreateSut()
    {
        var source = new FakeSegmentEmbeddingSourceRepository();
        var repository = new FakeSegmentEmbeddingRepository();
        var provider = new FakeEmbeddingProvider();
        var guard = new FakeSegmentEmbeddingSynchronizationGuard();

        var orchestrator = new EmbeddingOrchestrator(
            new[] { provider },
            Options.Create(new EmbeddingOptions { DefaultProvider = FakeEmbeddingProvider.ProviderName }));

        var syncOptions = Options.Create(new SegmentEmbeddingsSyncOptions
        {
            BatchSize = 50,
            MaxDegreeOfParallelism = 1,
            Locale = "pt-BR"
        });

        var handler = new SynchronizeSegmentEmbeddingsHandler(
            source,
            repository,
            orchestrator,
            guard,
            Options.Create(new EmbeddingOptions { DefaultProvider = FakeEmbeddingProvider.ProviderName }),
            syncOptions,
            NullLogger<SynchronizeSegmentEmbeddingsHandler>.Instance);

        return (handler, source, repository, provider, guard);
    }

    [Fact]
    public async Task HandleAsync_Should_Generate_Missing_Definition_Embedding()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        source.AddDefinition(CreateDefinition());

        var result = await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.DefinitionsRead);
        Assert.Equal(1, result.Generated);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.Equal(1, provider.CallCount);

        var row = Assert.Single(repository.Rows.Values);
        Assert.Equal("SegmentDefinition", row.EntityType);
        Assert.Equal(14, row.EntityId);
        Assert.NotNull(row.Embedding);
        Assert.Equal(row.ContentHash, row.EmbeddedContentHash);
    }

    [Fact]
    public async Task HandleAsync_Should_Generate_Missing_Option_Embedding()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        source.AddOption(CreateOption());

        var result = await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.OptionsRead);
        Assert.Equal(1, result.Generated);
        Assert.Equal(1, provider.CallCount);

        var row = Assert.Single(repository.Rows.Values);
        Assert.Equal("SegmentOption", row.EntityType);
        Assert.Equal(1401, row.EntityId);
        Assert.Equal(14, row.SegmentDefinitionId);
        Assert.Equal(1401, row.SegmentOptionId);
    }

    [Fact]
    public async Task HandleAsync_Should_Copy_AssignmentScope_To_Definition_And_Option()
    {
        var (handler, source, repository, _, _) = CreateSut();
        source.AddDefinition(CreateDefinition(assignmentScope: "ProductWithSkuOverride"));
        source.AddOption(CreateOption(assignmentScope: "ProductWithSkuOverride"));

        await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.All(repository.Rows.Values, row => Assert.Equal("ProductWithSkuOverride", row.AssignmentScope));
    }

    [Fact]
    public async Task HandleAsync_Should_Skip_Unchanged_Row_With_Valid_Embedding_And_Same_Provider()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        var definition = CreateDefinition();
        source.AddDefinition(definition);

        var semanticText = SegmentSemanticDocumentBuilder.BuildDefinitionText(definition);
        var contentHash = SegmentSemanticDocumentBuilder.ComputeContentHash(semanticText);

        repository.Seed(new FakeSegmentEmbeddingRepository.Row
        {
            Id = Guid.NewGuid(),
            EntityType = "SegmentDefinition",
            EntityId = 14,
            SegmentDefinitionId = 14,
            SegmentOptionId = null,
            SegmentCode = "gender",
            OptionCode = null,
            AssignmentScope = "ProductWithSkuOverride",
            Locale = "pt-BR",
            Name = "Gênero",
            SemanticText = semanticText,
            ContentHash = contentHash,
            EmbeddedContentHash = contentHash,
            Embedding = new float[1536],
            EmbeddingProvider = FakeEmbeddingProvider.ProviderName,
            EmbeddingModel = "fake-model",
            Metadata = "{}",
            IsActive = true
        });

        var result = await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

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

        repository.Seed(new FakeSegmentEmbeddingRepository.Row
        {
            Id = Guid.NewGuid(),
            EntityType = "SegmentDefinition",
            EntityId = 14,
            SegmentDefinitionId = 14,
            SegmentCode = "gender",
            AssignmentScope = "ProductWithSkuOverride",
            Locale = "pt-BR",
            Name = "Gênero",
            SemanticText = "old text",
            ContentHash = SegmentSemanticDocumentBuilder.ComputeContentHash("old text"),
            EmbeddedContentHash = SegmentSemanticDocumentBuilder.ComputeContentHash("old text"),
            Embedding = new float[1536],
            EmbeddingProvider = FakeEmbeddingProvider.ProviderName,
            EmbeddingModel = "fake-model",
            Metadata = "{}",
            IsActive = true
        });

        var result = await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task HandleAsync_Should_Regenerate_Row_When_Embedding_Is_Null()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        var definition = CreateDefinition();
        source.AddDefinition(definition);

        var semanticText = SegmentSemanticDocumentBuilder.BuildDefinitionText(definition);
        var contentHash = SegmentSemanticDocumentBuilder.ComputeContentHash(semanticText);

        repository.Seed(new FakeSegmentEmbeddingRepository.Row
        {
            Id = Guid.NewGuid(),
            EntityType = "SegmentDefinition",
            EntityId = 14,
            SegmentDefinitionId = 14,
            SegmentCode = "gender",
            AssignmentScope = "ProductWithSkuOverride",
            Locale = "pt-BR",
            Name = "Gênero",
            SemanticText = semanticText,
            ContentHash = contentHash,
            EmbeddedContentHash = null,
            Embedding = null,
            Metadata = "{}",
            IsActive = true
        });

        var result = await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task HandleAsync_Should_Regenerate_Row_When_Provider_Changes()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        var definition = CreateDefinition();
        source.AddDefinition(definition);

        var semanticText = SegmentSemanticDocumentBuilder.BuildDefinitionText(definition);
        var contentHash = SegmentSemanticDocumentBuilder.ComputeContentHash(semanticText);

        repository.Seed(new FakeSegmentEmbeddingRepository.Row
        {
            Id = Guid.NewGuid(),
            EntityType = "SegmentDefinition",
            EntityId = 14,
            SegmentDefinitionId = 14,
            SegmentCode = "gender",
            AssignmentScope = "ProductWithSkuOverride",
            Locale = "pt-BR",
            Name = "Gênero",
            SemanticText = semanticText,
            ContentHash = contentHash,
            EmbeddedContentHash = contentHash,
            Embedding = new float[1536],
            EmbeddingProvider = "some-other-provider",
            EmbeddingModel = "fake-model",
            Metadata = "{}",
            IsActive = true
        });

        var result = await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task HandleAsync_Should_Keep_Source_Pending_When_Provider_Fails()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        source.AddDefinition(CreateDefinition());
        provider.ThrowOnGenerate = true;

        var result = await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(0, result.Generated);

        var row = Assert.Single(repository.Rows.Values);
        Assert.Null(row.Embedding);
        Assert.Null(row.EmbeddedContentHash);
    }

    [Fact]
    public async Task HandleAsync_Should_Count_As_Failed_When_Optimistic_Completion_Is_Rejected()
    {
        var (handler, source, repository, _, _) = CreateSut();
        var definition = CreateDefinition();
        source.AddDefinition(definition);

        var semanticText = SegmentSemanticDocumentBuilder.BuildDefinitionText(definition);

        repository.Seed(new FakeSegmentEmbeddingRepository.Row
        {
            Id = Guid.NewGuid(),
            EntityType = "SegmentDefinition",
            EntityId = 14,
            SegmentDefinitionId = 14,
            SegmentCode = "gender",
            AssignmentScope = "ProductWithSkuOverride",
            Locale = "pt-BR",
            Name = "Gênero",
            SemanticText = semanticText,
            ContentHash = SegmentSemanticDocumentBuilder.ComputeContentHash(semanticText),
            EmbeddedContentHash = null,
            Embedding = null,
            Metadata = "{}",
            IsActive = true
        });

        // Simulates the source changing while the embedding provider call is
        // in flight: the content_hash observed before generation no longer
        // matches by the time CompleteAsync runs, so optimistic completion
        // must be rejected.
        repository.SimulateRaceOnNextCompleteKey = ("SegmentDefinition", 14L, "pt-BR");

        var result = await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(0, result.Generated);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public async Task HandleAsync_Should_Deactivate_Projections_No_Longer_Active()
    {
        var (handler, source, repository, _, _) = CreateSut();
        // Nothing active this run, but a row previously projected exists.
        repository.Seed(new FakeSegmentEmbeddingRepository.Row
        {
            Id = Guid.NewGuid(),
            EntityType = "SegmentDefinition",
            EntityId = 99,
            SegmentDefinitionId = 99,
            SegmentCode = "obsolete",
            AssignmentScope = "Product",
            Locale = "pt-BR",
            Name = "Obsoleto",
            SemanticText = "texto",
            ContentHash = SegmentSemanticDocumentBuilder.ComputeContentHash("texto"),
            EmbeddedContentHash = SegmentSemanticDocumentBuilder.ComputeContentHash("texto"),
            Embedding = new float[1536],
            EmbeddingProvider = FakeEmbeddingProvider.ProviderName,
            EmbeddingModel = "fake-model",
            Metadata = "{}",
            IsActive = true
        });

        var result = await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(1, result.Deactivated);
        Assert.False(repository.Rows[("SegmentDefinition", 99L, "pt-BR")].IsActive);
    }

    [Fact]
    public async Task HandleAsync_Should_Reactivate_Projection_That_Became_Active_Again()
    {
        var (handler, source, repository, _, _) = CreateSut();
        var definition = CreateDefinition();
        source.AddDefinition(definition);

        var semanticText = SegmentSemanticDocumentBuilder.BuildDefinitionText(definition);
        var contentHash = SegmentSemanticDocumentBuilder.ComputeContentHash(semanticText);

        repository.Seed(new FakeSegmentEmbeddingRepository.Row
        {
            Id = Guid.NewGuid(),
            EntityType = "SegmentDefinition",
            EntityId = 14,
            SegmentDefinitionId = 14,
            SegmentCode = "gender",
            AssignmentScope = "ProductWithSkuOverride",
            Locale = "pt-BR",
            Name = "Gênero",
            SemanticText = semanticText,
            ContentHash = contentHash,
            EmbeddedContentHash = contentHash,
            Embedding = new float[1536],
            EmbeddingProvider = FakeEmbeddingProvider.ProviderName,
            EmbeddingModel = "fake-model",
            Metadata = "{}",
            IsActive = false
        });

        await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.True(repository.Rows[("SegmentDefinition", 14L, "pt-BR")].IsActive);
    }

    [Fact]
    public async Task HandleAsync_Should_Prevent_Concurrent_Synchronization()
    {
        var (handler, _, _, _, guard) = CreateSut();
        guard.AlwaysBusy = true;

        await Assert.ThrowsAsync<SegmentEmbeddingSynchronizationInProgressException>(
            () => handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_Should_Use_Explicit_Provider_When_Given()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        source.AddDefinition(CreateDefinition());

        var result = await handler.HandleAsync(
            new SynchronizeSegmentEmbeddingsCommand(FakeEmbeddingProvider.ProviderName, null),
            CancellationToken.None);

        Assert.Equal(FakeEmbeddingProvider.ProviderName, result.Provider);
    }

    [Fact]
    public async Task HandleAsync_Should_Use_Default_Provider_When_Not_Given()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        source.AddDefinition(CreateDefinition());

        var result = await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(FakeEmbeddingProvider.ProviderName, result.Provider);
    }

    [Fact]
    public async Task HandleAsync_Should_Report_Correct_Counts()
    {
        var (handler, source, repository, _, _) = CreateSut();
        source.AddDefinition(CreateDefinition("gender"));
        source.AddOption(CreateOption());

        var upToDateDefinition = CreateDefinition("sport_modality", assignmentScope: "Product", segmentDefinitionId: 20);
        source.AddDefinition(upToDateDefinition);

        var upToDateText = SegmentSemanticDocumentBuilder.BuildDefinitionText(upToDateDefinition);
        var upToDateHash = SegmentSemanticDocumentBuilder.ComputeContentHash(upToDateText);

        repository.Seed(new FakeSegmentEmbeddingRepository.Row
        {
            Id = Guid.NewGuid(),
            EntityType = "SegmentDefinition",
            EntityId = 20,
            SegmentDefinitionId = 20,
            SegmentCode = "sport_modality",
            AssignmentScope = "Product",
            Locale = "pt-BR",
            Name = "Gênero",
            SemanticText = upToDateText,
            ContentHash = upToDateHash,
            EmbeddedContentHash = upToDateHash,
            Embedding = new float[1536],
            EmbeddingProvider = FakeEmbeddingProvider.ProviderName,
            EmbeddingModel = "fake-model",
            Metadata = "{}",
            IsActive = true
        });

        var result = await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(2, result.DefinitionsRead);
        Assert.Equal(1, result.OptionsRead);
        Assert.Equal(2, result.Generated); // gender definition + option
        Assert.Equal(0, result.Updated);
        Assert.Equal(1, result.Skipped); // sport_modality definition
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task Second_Run_Without_Changes_Should_Make_Zero_Provider_Calls_And_Skip_Everything()
    {
        var (handler, source, repository, provider, _) = CreateSut();
        source.AddDefinition(CreateDefinition());
        source.AddOption(CreateOption());

        var firstResult = await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(2, firstResult.Generated);
        Assert.Equal(2, provider.CallCount);

        var secondResult = await handler.HandleAsync(new SynchronizeSegmentEmbeddingsCommand(null, null), CancellationToken.None);

        Assert.Equal(0, secondResult.Generated);
        Assert.Equal(0, secondResult.Updated);
        Assert.Equal(2, secondResult.Skipped);
        Assert.Equal(0, secondResult.Failed);
        Assert.Equal(2, provider.CallCount); // no additional calls
    }
}
