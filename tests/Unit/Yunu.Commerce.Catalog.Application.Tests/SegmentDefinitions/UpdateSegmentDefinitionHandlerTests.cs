using Xunit;
using Yunu.Commerce.Catalog.Application.SegmentDefinitions;
using Yunu.Commerce.Catalog.Application.SegmentDefinitions.CreateSegmentDefinition;
using Yunu.Commerce.Catalog.Application.SegmentDefinitions.UpdateSegmentDefinition;

namespace Yunu.Commerce.Catalog.Application.Tests.SegmentDefinitions;

public class UpdateSegmentDefinitionHandlerTests
{
    private static UpdateSegmentDefinitionHandler CreateHandler(
        FakeSegmentDefinitionRepository repo,
        FakeSegmentDefinitionUsageReader? usageReader = null,
        FakeProductRepository? productRepository = null,
        FakeSkuRepository? skuRepository = null) => new(
            repo,
            usageReader ?? new FakeSegmentDefinitionUsageReader(),
            productRepository ?? new FakeProductRepository(),
            skuRepository ?? new FakeSkuRepository());

    private static async Task<long> CreateDefinitionAsync(FakeSegmentDefinitionRepository repo, string code = "gender", string name = "Gender")
    {
        var createHandler = new CreateSegmentDefinitionHandler(repo);
        var result = await createHandler.HandleAsync(new CreateSegmentDefinitionCommand
        {
            Code = code,
            Name = name,
            Description = null,
            SemanticText = null,
            SelectionMode = "Single",
            AssignmentScope = "Product"
        }, CancellationToken.None);

        return result.SegmentDefinitionId;
    }

    private static UpdateSegmentDefinitionCommand UpdateCommand(long id, string name = "Gender", string status = "Draft", string selectionMode = "Single", string assignmentScope = "Product") => new()
    {
        SegmentDefinitionId = id,
        Name = name,
        Description = null,
        SemanticText = null,
        SelectionMode = selectionMode,
        AssignmentScope = assignmentScope,
        Status = status
    };

    [Fact]
    public async Task Update_allows_permitted_fields()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var id = await CreateDefinitionAsync(repo);
        var handler = CreateHandler(repo);

        await handler.HandleAsync(UpdateCommand(id, name: "New Gender", status: "Active"), CancellationToken.None);

        var updated = await repo.GetByIdAsync(new Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId(id), CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("New Gender", updated!.Name.Value);
        Assert.Equal(Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionStatus.Active, updated.Status);
    }

    [Fact]
    public async Task Update_nonexistent_id_returns_error()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var handler = CreateHandler(repo);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.HandleAsync(UpdateCommand(9999), CancellationToken.None));
    }

    [Fact]
    public async Task Update_rejects_invalid_status()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var id = await CreateDefinitionAsync(repo);
        var handler = CreateHandler(repo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(UpdateCommand(id, status: "NotAStatus"), CancellationToken.None));
    }

    [Fact]
    public async Task Update_rejects_duplicate_normalized_name()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var firstId = await CreateDefinitionAsync(repo, code: "gender", name: "Gender");
        var secondId = await CreateDefinitionAsync(repo, code: "audience", name: "Audience");
        var handler = CreateHandler(repo);

        await Assert.ThrowsAsync<SegmentDefinitionConflictException>(() =>
            handler.HandleAsync(UpdateCommand(secondId, name: "Gender"), CancellationToken.None));
    }

    [Fact]
    public async Task Update_command_has_no_code_property()
    {
        // The Command's shape itself guarantees Code cannot be changed via Update:
        // there is no Code property to set. This test documents that invariant.
        var properties = typeof(UpdateSegmentDefinitionCommand).GetProperties();
        Assert.DoesNotContain(properties, p => p.Name == "Code");
    }

    [Fact]
    public async Task Update_structural_change_outside_draft_is_rejected()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var id = await CreateDefinitionAsync(repo);
        var handler = CreateHandler(repo);

        // Move to Active first.
        await handler.HandleAsync(UpdateCommand(id, status: "Active"), CancellationToken.None);

        await Assert.ThrowsAsync<Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionStructuralChangeNotAllowedException>(() =>
            handler.HandleAsync(UpdateCommand(id, status: "Active", selectionMode: "Multiple"), CancellationToken.None));
    }

    [Fact]
    public void Repository_interface_has_no_delete_method()
    {
        var methods = typeof(Yunu.Commerce.Catalog.Domain.Segments.ISegmentDefinitionRepository).GetMethods();
        Assert.DoesNotContain(methods, m => m.Name.Contains("Delete"));
    }

    [Fact]
    public async Task Update_archive_with_no_usage_is_allowed()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var id = await CreateDefinitionAsync(repo);
        var handler = CreateHandler(repo);

        await handler.HandleAsync(UpdateCommand(id, status: "Archived"), CancellationToken.None);

        var updated = await repo.GetByIdAsync(new Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId(id), CancellationToken.None);
        Assert.Equal(Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionStatus.Archived, updated!.Status);
    }

    [Fact]
    public async Task Update_archive_blocked_by_approved_canonical_association()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var id = await CreateDefinitionAsync(repo);
        var usageReader = new FakeSegmentDefinitionUsageReader();
        usageReader.MarkApprovedAssociationInUse(new Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId(id));
        var handler = CreateHandler(repo, usageReader: usageReader);

        await Assert.ThrowsAsync<SegmentDefinitionInUseException>(() =>
            handler.HandleAsync(UpdateCommand(id, status: "Archived"), CancellationToken.None));
    }

    [Fact]
    public async Task Update_archive_blocked_by_product_usage()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var id = await CreateDefinitionAsync(repo);
        var productRepository = new FakeProductRepository();
        productRepository.MarkSegmentDefinitionInUse(new Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId(id));
        var handler = CreateHandler(repo, productRepository: productRepository);

        await Assert.ThrowsAsync<SegmentDefinitionInUseException>(() =>
            handler.HandleAsync(UpdateCommand(id, status: "Archived"), CancellationToken.None));
    }

    [Fact]
    public async Task Update_archive_blocked_by_sku_usage()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var id = await CreateDefinitionAsync(repo);
        var skuRepository = new FakeSkuRepository();
        skuRepository.MarkSegmentDefinitionInUse(new Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId(id));
        var handler = CreateHandler(repo, skuRepository: skuRepository);

        await Assert.ThrowsAsync<SegmentDefinitionInUseException>(() =>
            handler.HandleAsync(UpdateCommand(id, status: "Archived"), CancellationToken.None));
    }
}
