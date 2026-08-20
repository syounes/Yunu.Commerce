using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.SegmentDefinitions.UpdateSegmentDefinition;

/// <summary>
/// Orchestrates update of a Segment Definition, including its lifecycle
/// transitions (docs task: "Yunu.Commerce V8 - Lifecycle + Usage Guards de
/// Segments"). An Active/Inactive -> Archived transition is blocked when the
/// Definition is still in effective use: an Approved Canonical Taxonomy
/// association, or a Product/Sku Segment assignment referencing it. Only
/// Archive is guarded; Active <-> Inactive is a reversible operational
/// suspension and is intentionally not blocked by usage (docs task,
/// "Active -> Inactive" policy).
/// </summary>
public sealed class UpdateSegmentDefinitionHandler
{
    private readonly ISegmentDefinitionRepository _repository;
    private readonly ISegmentDefinitionUsageReader _usageReader;
    private readonly IProductRepository _productRepository;
    private readonly ISkuRepository _skuRepository;

    public UpdateSegmentDefinitionHandler(
        ISegmentDefinitionRepository repository,
        ISegmentDefinitionUsageReader usageReader,
        IProductRepository productRepository,
        ISkuRepository skuRepository)
    {
        _repository = repository;
        _usageReader = usageReader;
        _productRepository = productRepository;
        _skuRepository = skuRepository;
    }

    public async Task HandleAsync(UpdateSegmentDefinitionCommand command, CancellationToken cancellationToken)
    {
        var id = new SegmentDefinitionId(command.SegmentDefinitionId);

        var definition = await _repository.GetByIdAsync(id, cancellationToken);
        if (definition is null)
        {
            throw new KeyNotFoundException($"SegmentDefinition '{command.SegmentDefinitionId}' not found.");
        }

        var selectionMode = ParseEnum<SegmentSelectionMode>(command.SelectionMode, nameof(command.SelectionMode));
        var assignmentScope = ParseEnum<SegmentAssignmentScope>(command.AssignmentScope, nameof(command.AssignmentScope));
        var status = ParseEnum<SegmentDefinitionStatus>(command.Status, nameof(command.Status));

        var name = new SegmentDefinitionName(command.Name);
        var normalizedName = SegmentTextNormalizer.Normalize(name.Value);

        var existingWithSameName = await _repository.FindByNormalizedNameAsync(normalizedName, cancellationToken);
        if (existingWithSameName is not null && existingWithSameName.Id != definition.Id)
        {
            throw new SegmentDefinitionConflictException($"SegmentDefinition with name '{name.Value}' already exists.");
        }

        if (status == SegmentDefinitionStatus.Archived && definition.Status != SegmentDefinitionStatus.Archived)
        {
            await EnsureNotInUseAsync(id, cancellationToken);
        }

        definition.Update(
            name,
            command.Description,
            command.SemanticText,
            selectionMode,
            assignmentScope,
            status);

        await _repository.UpdateAsync(definition, cancellationToken);
    }

    private async Task EnsureNotInUseAsync(SegmentDefinitionId id, CancellationToken cancellationToken)
    {
        if (await _usageReader.HasApprovedCanonicalTaxonomyAssociationAsync(id, cancellationToken))
        {
            throw new SegmentDefinitionInUseException(
                $"SegmentDefinition '{id.Value}' has at least one Approved Canonical Taxonomy association and cannot be archived.");
        }

        if (await _productRepository.ExistsBySegmentDefinitionIdAsync(id, cancellationToken))
        {
            throw new SegmentDefinitionInUseException(
                $"SegmentDefinition '{id.Value}' is used by at least one Product and cannot be archived.");
        }

        if (await _skuRepository.ExistsBySegmentDefinitionIdAsync(id, cancellationToken))
        {
            throw new SegmentDefinitionInUseException(
                $"SegmentDefinition '{id.Value}' is used by at least one Sku and cannot be archived.");
        }
    }

    private static TEnum ParseEnum<TEnum>(string value, string paramName) where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
        {
            throw new ArgumentException($"Invalid {paramName}: '{value}'.", paramName);
        }

        return parsed;
    }
}
