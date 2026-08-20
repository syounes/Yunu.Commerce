using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.SegmentOptions.UpdateSegmentOption;

/// <summary>
/// Orchestrates update of a Segment Option (docs task: "Implementar Domain +
/// Write-Side de SegmentOption"), mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.SegmentDefinitions.UpdateSegmentDefinition.UpdateSegmentDefinitionHandler"/>.
/// SegmentDefinitionId is never accepted here: an Option cannot be moved to
/// a different Definition (see <see cref="SegmentOption"/> remarks).
///
/// Usage guard (docs task: "Yunu.Commerce V8 - Lifecycle + Usage Guards de
/// Segments"): archiving an Option in active use by at least one Product or
/// Sku Segment assignment is blocked, mirroring the SegmentDefinition
/// Archive guard. Only Archive is guarded; no cascade is performed on the
/// existing assignments.
/// </summary>
public sealed class UpdateSegmentOptionHandler
{
    private readonly ISegmentOptionRepository _repository;
    private readonly IProductRepository _productRepository;
    private readonly ISkuRepository _skuRepository;

    public UpdateSegmentOptionHandler(
        ISegmentOptionRepository repository,
        IProductRepository productRepository,
        ISkuRepository skuRepository)
    {
        _repository = repository;
        _productRepository = productRepository;
        _skuRepository = skuRepository;
    }

    public async Task HandleAsync(UpdateSegmentOptionCommand command, CancellationToken cancellationToken)
    {
        var id = new SegmentOptionId(command.SegmentOptionId);

        var option = await _repository.GetByIdAsync(id, cancellationToken);
        if (option is null)
        {
            throw new KeyNotFoundException($"SegmentOption '{command.SegmentOptionId}' not found.");
        }

        var status = ParseEnum<SegmentOptionStatus>(command.Status, nameof(command.Status));

        var name = new SegmentOptionName(command.Name);
        var normalizedName = SegmentTextNormalizer.Normalize(name.Value);

        var existingWithSameName = await _repository.FindByNormalizedNameAsync(option.SegmentDefinitionId, normalizedName, cancellationToken);
        if (existingWithSameName is not null && existingWithSameName.Id != option.Id)
        {
            throw new SegmentOptionConflictException(
                $"SegmentOption with name '{name.Value}' already exists for SegmentDefinition '{option.SegmentDefinitionId.Value}'.");
        }

        if (status == SegmentOptionStatus.Archived && option.Status != SegmentOptionStatus.Archived)
        {
            await EnsureNotInUseAsync(id, cancellationToken);
        }

        option.Update(
            name,
            command.Description,
            command.SemanticText,
            command.DisplayOrder,
            status);

        await _repository.UpdateAsync(option, cancellationToken);
    }

    private async Task EnsureNotInUseAsync(SegmentOptionId id, CancellationToken cancellationToken)
    {
        if (await _productRepository.ExistsBySegmentOptionIdAsync(id, cancellationToken))
        {
            throw new SegmentOptionInUseException(
                $"SegmentOption '{id.Value}' is used by at least one Product and cannot be archived.");
        }

        if (await _skuRepository.ExistsBySegmentOptionIdAsync(id, cancellationToken))
        {
            throw new SegmentOptionInUseException(
                $"SegmentOption '{id.Value}' is used by at least one Sku and cannot be archived.");
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
