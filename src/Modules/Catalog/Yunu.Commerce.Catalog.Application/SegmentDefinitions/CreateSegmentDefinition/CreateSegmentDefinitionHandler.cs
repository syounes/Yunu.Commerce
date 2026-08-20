using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Application.SegmentDefinitions.CreateSegmentDefinition;

public sealed class CreateSegmentDefinitionHandler
{
    private readonly ISegmentDefinitionRepository _repository;

    public CreateSegmentDefinitionHandler(ISegmentDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<CreateSegmentDefinitionResult> HandleAsync(CreateSegmentDefinitionCommand command, CancellationToken cancellationToken)
    {
        var code = new SegmentDefinitionCode(command.Code);

        if (await _repository.ExistsCodeAsync(code, cancellationToken))
        {
            throw new SegmentDefinitionConflictException($"SegmentDefinition with code '{code.Value}' already exists.");
        }

        var selectionMode = ParseEnum<SegmentSelectionMode>(command.SelectionMode, nameof(command.SelectionMode));
        var assignmentScope = ParseEnum<SegmentAssignmentScope>(command.AssignmentScope, nameof(command.AssignmentScope));

        var name = new SegmentDefinitionName(command.Name);

        var definition = SegmentDefinition.Create(
            code,
            name,
            command.Description,
            command.SemanticText,
            selectionMode,
            assignmentScope,
            command.IsRequired);

        if (await _repository.FindByNormalizedNameAsync(definition.NormalizedName, cancellationToken) is not null)
        {
            throw new SegmentDefinitionConflictException($"SegmentDefinition with name '{name.Value}' already exists.");
        }

        var id = await _repository.AddAsync(definition, cancellationToken);

        return new CreateSegmentDefinitionResult { SegmentDefinitionId = id.Value };
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
