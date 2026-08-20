using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Application.SegmentOptions.UpdateSegmentOption;

/// <summary>
/// Orchestrates update of a Segment Option (docs task: "Implementar Domain +
/// Write-Side de SegmentOption"), mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.SegmentDefinitions.UpdateSegmentDefinition.UpdateSegmentDefinitionHandler"/>.
/// SegmentDefinitionId is never accepted here: an Option cannot be moved to
/// a different Definition (see <see cref="SegmentOption"/> remarks).
/// </summary>
public sealed class UpdateSegmentOptionHandler
{
    private readonly ISegmentOptionRepository _repository;

    public UpdateSegmentOptionHandler(ISegmentOptionRepository repository)
    {
        _repository = repository;
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

        option.Update(
            name,
            command.Description,
            command.SemanticText,
            command.DisplayOrder,
            status);

        await _repository.UpdateAsync(option, cancellationToken);
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
