using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Application.SegmentDefinitions.UpdateSegmentDefinition;

public sealed class UpdateSegmentDefinitionHandler
{
    private readonly ISegmentDefinitionRepository _repository;

    public UpdateSegmentDefinitionHandler(ISegmentDefinitionRepository repository)
    {
        _repository = repository;
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

        definition.Update(
            name,
            command.Description,
            command.SemanticText,
            selectionMode,
            assignmentScope,
            command.IsRequired,
            status);

        await _repository.UpdateAsync(definition, cancellationToken);
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
