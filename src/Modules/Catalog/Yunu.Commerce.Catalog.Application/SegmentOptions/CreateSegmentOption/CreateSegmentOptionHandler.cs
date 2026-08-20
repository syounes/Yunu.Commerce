using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Application.SegmentOptions.CreateSegmentOption;

/// <summary>
/// Orchestrates creation of a Segment Option (docs task: "Implementar
/// Domain + Write-Side de SegmentOption"), mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.SegmentDefinitions.CreateSegmentDefinition.CreateSegmentDefinitionHandler"/>.
///
/// Parent lifecycle policy (docs task: "Yunu.Commerce V8 - Lifecycle +
/// Usage Guards de Segments" - "Regra minima obrigatoria"): a new Option
/// cannot be created under an Archived parent Definition, since Archived is
/// terminal and must not receive new structural values. Draft/Active/
/// Inactive parents are all allowed to receive new Options: the project
/// does not define a stricter policy for Draft/Inactive, and blocking only
/// Archived is the smallest rule that prevents an evident inconsistency.
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

        if (definition.Status == SegmentDefinitionStatus.Archived)
        {
            throw new SegmentDefinitionArchivedException(
                $"SegmentDefinition '{command.SegmentDefinitionId}' is Archived and cannot receive new SegmentOptions.");
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
