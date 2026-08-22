using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

namespace Yunu.Commerce.Catalog.Application.Tests.SourceTaxonomy;

public class SourceTaxonomyImportOrchestratorTests
{
    private static SourceTaxonomyDescriptorRecord Descriptor(
        long id = 1,
        string providerCode = "google",
        string? scopeCode = null,
        string? externalTaxonomyId = null,
        bool isActive = true) => new()
    {
        SourceTaxonomyId = id,
        Code = $"code-{id}",
        Name = $"Name {id}",
        ProviderCode = providerCode,
        ScopeCode = scopeCode,
        ExternalTaxonomyId = externalTaxonomyId,
        ExternalVersion = null,
        DefaultLanguage = "pt-BR",
        SourceUri = null,
        SourceChecksum = null,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = null,
        ImportedAt = DateTime.UtcNow
    };

    private static SourceTaxonomySnapshot ValidSnapshot(
        string providerCode = "google",
        string? scopeCode = null,
        string? externalTaxonomyId = null) => new()
    {
        Descriptor = new SourceTaxonomySnapshotDescriptor
        {
            ProviderCode = providerCode,
            ScopeCode = scopeCode,
            ExternalTaxonomyId = externalTaxonomyId,
            ExternalVersion = "v1",
            Locale = "pt-BR",
            SourceUri = "https://example.com",
            SourceChecksum = "abc"
        },
        Nodes = new[]
        {
            new SourceTaxonomySnapshotNode
            {
                ExternalNodeId = "1",
                ParentExternalNodeId = null,
                NodeType = "Category",
                Name = "Root",
                FullPath = "Root",
                Level = 0,
                IsLeaf = true,
                IsActive = true
            }
        }
    };

    private static SourceTaxonomySynchronizationResult SuccessResult() => new()
    {
        NodeCount = 1,
        InsertedCount = 1,
        UpdatedCount = 0,
        DeactivatedCount = 0,
        WasSkippedByChecksum = false
    };

    private static SourceTaxonomyImportOrchestrator CreateOrchestrator(
        FakeSourceTaxonomyRepository repository,
        IEnumerable<ISourceTaxonomyAdapter> adapters,
        FakeSourceTaxonomyImportStore importStore,
        ISourceTaxonomySynchronizationStore synchronizationStore,
        FakeSourceTaxonomyImportGuard? guard = null)
    {
        return new SourceTaxonomyImportOrchestrator(
            repository,
            adapters,
            importStore,
            synchronizationStore,
            guard ?? new FakeSourceTaxonomyImportGuard(),
            NullLogger<SourceTaxonomyImportOrchestrator>.Instance);
    }

    [Fact]
    public async Task ImportAsync_With_MissingSourceTaxonomy_Should_Throw()
    {
        var repository = new FakeSourceTaxonomyRepository();
        var orchestrator = CreateOrchestrator(
            repository,
            Array.Empty<ISourceTaxonomyAdapter>(),
            new FakeSourceTaxonomyImportStore(),
            new FakeSourceTaxonomySynchronizationStore(SuccessResult()));

        await Assert.ThrowsAsync<SourceTaxonomyNotFoundException>(
            () => orchestrator.ImportAsync(1, "adapter", CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_With_InactiveSourceTaxonomy_Should_Throw()
    {
        var repository = new FakeSourceTaxonomyRepository();
        repository.Seed(Descriptor(isActive: false));

        var orchestrator = CreateOrchestrator(
            repository,
            Array.Empty<ISourceTaxonomyAdapter>(),
            new FakeSourceTaxonomyImportStore(),
            new FakeSourceTaxonomySynchronizationStore(SuccessResult()));

        await Assert.ThrowsAsync<SourceTaxonomyInactiveException>(
            () => orchestrator.ImportAsync(1, "adapter", CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_With_MissingAdapter_Should_Throw()
    {
        var repository = new FakeSourceTaxonomyRepository();
        repository.Seed(Descriptor());

        var orchestrator = CreateOrchestrator(
            repository,
            Array.Empty<ISourceTaxonomyAdapter>(),
            new FakeSourceTaxonomyImportStore(),
            new FakeSourceTaxonomySynchronizationStore(SuccessResult()));

        await Assert.ThrowsAsync<SourceTaxonomyAdapterNotFoundException>(
            () => orchestrator.ImportAsync(1, "missing-adapter", CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_With_DuplicateAdapterRegistrations_Should_Throw()
    {
        var repository = new FakeSourceTaxonomyRepository();
        repository.Seed(Descriptor());

        var adapters = new ISourceTaxonomyAdapter[]
        {
            new FakeSourceTaxonomyAdapter("dup", _ => ValidSnapshot()),
            new FakeSourceTaxonomyAdapter("dup", _ => ValidSnapshot())
        };

        var orchestrator = CreateOrchestrator(
            repository,
            adapters,
            new FakeSourceTaxonomyImportStore(),
            new FakeSourceTaxonomySynchronizationStore(SuccessResult()));

        await Assert.ThrowsAsync<SourceTaxonomyDuplicateAdapterException>(
            () => orchestrator.ImportAsync(1, "dup", CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_Should_Call_Adapter_With_ProviderNeutral_Context()
    {
        var repository = new FakeSourceTaxonomyRepository();
        repository.Seed(Descriptor(scopeCode: "BR", externalTaxonomyId: "ext-1"));

        var adapter = new FakeSourceTaxonomyAdapter("adapter", _ => ValidSnapshot(scopeCode: "BR", externalTaxonomyId: "ext-1"));

        var orchestrator = CreateOrchestrator(
            repository,
            new[] { adapter },
            new FakeSourceTaxonomyImportStore(),
            new FakeSourceTaxonomySynchronizationStore(SuccessResult()));

        await orchestrator.ImportAsync(1, "adapter", CancellationToken.None);

        Assert.NotNull(adapter.LastContext);
        Assert.Equal(1, adapter.LastContext!.SourceTaxonomyId);
        Assert.Equal("google", adapter.LastContext.ProviderCode);
        Assert.Equal("BR", adapter.LastContext.ScopeCode);
        Assert.Equal("ext-1", adapter.LastContext.ExternalTaxonomyId);
    }

    [Fact]
    public async Task ImportAsync_With_ProviderMismatch_Should_Throw()
    {
        var repository = new FakeSourceTaxonomyRepository();
        repository.Seed(Descriptor(providerCode: "google"));

        var adapter = new FakeSourceTaxonomyAdapter("adapter", _ => ValidSnapshot(providerCode: "mercadolivre"));

        var orchestrator = CreateOrchestrator(
            repository,
            new[] { adapter },
            new FakeSourceTaxonomyImportStore(),
            new FakeSourceTaxonomySynchronizationStore(SuccessResult()));

        await Assert.ThrowsAsync<SourceTaxonomyProviderMismatchException>(
            () => orchestrator.ImportAsync(1, "adapter", CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_With_ScopeCodeConflict_Should_Throw()
    {
        var repository = new FakeSourceTaxonomyRepository();
        repository.Seed(Descriptor(scopeCode: "BR"));

        var adapter = new FakeSourceTaxonomyAdapter("adapter", _ => ValidSnapshot(scopeCode: "US"));

        var orchestrator = CreateOrchestrator(
            repository,
            new[] { adapter },
            new FakeSourceTaxonomyImportStore(),
            new FakeSourceTaxonomySynchronizationStore(SuccessResult()));

        await Assert.ThrowsAsync<SourceTaxonomyScopeConflictException>(
            () => orchestrator.ImportAsync(1, "adapter", CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_With_ExternalTaxonomyIdConflict_Should_Throw()
    {
        var repository = new FakeSourceTaxonomyRepository();
        repository.Seed(Descriptor(externalTaxonomyId: "ext-1"));

        var adapter = new FakeSourceTaxonomyAdapter("adapter", _ => ValidSnapshot(externalTaxonomyId: "ext-2"));

        var orchestrator = CreateOrchestrator(
            repository,
            new[] { adapter },
            new FakeSourceTaxonomyImportStore(),
            new FakeSourceTaxonomySynchronizationStore(SuccessResult()));

        await Assert.ThrowsAsync<SourceTaxonomyExternalTaxonomyIdConflictException>(
            () => orchestrator.ImportAsync(1, "adapter", CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_With_AdapterException_Should_Record_Failed_Import()
    {
        var repository = new FakeSourceTaxonomyRepository();
        repository.Seed(Descriptor());

        var adapter = new FakeSourceTaxonomyAdapter("adapter", new InvalidOperationException("boom"));
        var importStore = new FakeSourceTaxonomyImportStore();

        var orchestrator = CreateOrchestrator(
            repository,
            new[] { adapter },
            importStore,
            new FakeSourceTaxonomySynchronizationStore(SuccessResult()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.ImportAsync(1, "adapter", CancellationToken.None));

        Assert.Single(importStore.FailedImports);
        Assert.Contains("boom", importStore.FailedImports[0].ErrorMessage);
    }

    [Fact]
    public async Task ImportAsync_With_ValidationFailure_Should_Record_Failed_Import()
    {
        var repository = new FakeSourceTaxonomyRepository();
        repository.Seed(Descriptor());

        var invalidSnapshot = new SourceTaxonomySnapshot
        {
            Descriptor = ValidSnapshot().Descriptor,
            Nodes = Array.Empty<SourceTaxonomySnapshotNode>()
        };

        var adapter = new FakeSourceTaxonomyAdapter("adapter", _ => invalidSnapshot);
        var importStore = new FakeSourceTaxonomyImportStore();

        var orchestrator = CreateOrchestrator(
            repository,
            new[] { adapter },
            importStore,
            new FakeSourceTaxonomySynchronizationStore(SuccessResult()));

        await Assert.ThrowsAsync<SourceTaxonomySnapshotValidationException>(
            () => orchestrator.ImportAsync(1, "adapter", CancellationToken.None));

        Assert.Single(importStore.FailedImports);
    }

    [Fact]
    public async Task ImportAsync_When_MarkFailedAsync_Throws_Should_Preserve_Original_Exception()
    {
        var repository = new FakeSourceTaxonomyRepository();
        repository.Seed(Descriptor());

        var originalException = new InvalidOperationException("original failure");
        var adapter = new FakeSourceTaxonomyAdapter("adapter", originalException);

        var markFailedException = new TimeoutException("secondary failure while marking failed");
        var importStore = new FakeSourceTaxonomyImportStore(markFailedException);

        var orchestrator = CreateOrchestrator(
            repository,
            new[] { adapter },
            importStore,
            new FakeSourceTaxonomySynchronizationStore(SuccessResult()));

        var observed = await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.ImportAsync(1, "adapter", CancellationToken.None));

        Assert.Same(originalException, observed);
    }

    [Fact]
    public async Task ImportAsync_With_AlreadyCancelledToken_After_Started_Should_Still_Attempt_Failure_Cleanup()
    {
        var repository = new FakeSourceTaxonomyRepository();
        repository.Seed(Descriptor());

        var adapter = new FakeSourceTaxonomyAdapter("adapter", new InvalidOperationException("boom"));
        var importStore = new FakeSourceTaxonomyImportStore();

        var orchestrator = CreateOrchestrator(
            repository,
            new[] { adapter },
            importStore,
            new FakeSourceTaxonomySynchronizationStore(SuccessResult()));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.ImportAsync(1, "adapter", cts.Token));

        // Failure cleanup must not be skipped merely because the caller
        // token is already cancelled: the persisted Started row must be
        // marked Failed using a cleanup-safe token.
        Assert.Single(importStore.FailedImports);
        Assert.All(importStore.MarkFailedCancellationTokens, token => Assert.False(token.IsCancellationRequested));
    }

    [Fact]
    public async Task ImportAsync_Should_Return_Synchronization_Counts_On_Success()
    {
        var repository = new FakeSourceTaxonomyRepository();
        repository.Seed(Descriptor());

        var adapter = new FakeSourceTaxonomyAdapter("adapter", _ => ValidSnapshot());

        var result = SuccessResult();

        var orchestrator = CreateOrchestrator(
            repository,
            new[] { adapter },
            new FakeSourceTaxonomyImportStore(),
            new FakeSourceTaxonomySynchronizationStore(result));

        var importResult = await orchestrator.ImportAsync(1, "adapter", CancellationToken.None);

        Assert.Equal(1, importResult.NodeCount);
        Assert.Equal(1, importResult.InsertedCount);
        Assert.Equal(0, importResult.UpdatedCount);
        Assert.Equal(0, importResult.DeactivatedCount);
        Assert.Equal("adapter", importResult.AdapterCode);
    }
}
