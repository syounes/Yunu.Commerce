using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Application.SegmentOptions.CreateSegmentOption;

/// <summary>
/// Orchestrates creation of a Segment Option (docs task: "Implementar
/// Domain + Write-Side de SegmentOption"), mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.SegmentDefinitions.CreateSegmentDefinition.CreateSegmentDefinitionHandler"/>.
///
/// Does not enforce a policy based on the parent SegmentDefinition's
/// Status (e.g. rejecting creation under a non-Active Definition): the
/// project does not yet define that policy, so this handler intentionally
/// only requires the parent Definition to exist.
/// </summary>
public sealed class CreateSegmentOptionHandler
{
    private readonly ISegmentOptionRepository _repository;
    private readonly ISegmentDefinitionRepository _definitionRepository;

    public CreateSegmentOptionHandler(
        ISegmentOptionRepository repository,
        ISegmentDefinitionRepository definitionRepository)
    {
        _repository = repository;
        _definitionRepository = definitionRepository;
    }

    public async Task<CreateSegmentOptionResult> HandleAsync(CreateSegmentOptionCommand command, CancellationToken cancellationToken)
    {
        var segmentDefinitionId = new SegmentDefinitionId(command.SegmentDefinitionId);

        var definition = await _definitionRepository.GetByIdAsync(segmentDefinitionId, cancellationToken);
        if (definition is null)
        {
            throw new KeyNotFoundException($"SegmentDefinition '{command.SegmentDefinitionId}' not found.");
        }

        var code = new SegmentOptionCode(command.Code);

        if (await _repository.ExistsCodeAsync(segmentDefinitionId, code, cancellationToken))
        {
            throw new SegmentOptionConflictException(
                $"SegmentOption with code '{code.Value}' already exists for SegmentDefinition '{command.SegmentDefinitionId}'.");
        }

        var name = new SegmentOptionName(command.Name);

        var option = SegmentOption.Create(
            segmentDefinitionId,
            code,
            name,
            command.Description,
            command.SemanticText,
            command.DisplayOrder);

        if (await _repository.FindByNormalizedNameAsync(segmentDefinitionId, option.NormalizedName, cancellationToken) is not null)
        {
            throw new SegmentOptionConflictException(
                $"SegmentOption with name '{name.Value}' already exists for SegmentDefinition '{command.SegmentDefinitionId}'.");
        }

        var id = await _repository.AddAsync(option, cancellationToken);

        return new CreateSegmentOptionResult { SegmentOptionId = id.Value };
    }
}
