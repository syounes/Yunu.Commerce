using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Application.SegmentCatalog;

/// <summary>
/// Resolved, validated Segment assignment ready to be applied to a Product or
/// Sku Aggregate (docs task: "Canonical Taxonomy + Segments Domain" §28, §30).
/// </summary>
public sealed record ResolvedSegmentAssignment(
    SegmentDefinitionId SegmentDefinitionId,
    string SegmentCode,
    SegmentAssignmentScope AssignmentScope,
    IReadOnlyCollection<SegmentOptionSelection> Options);

/// <summary>
/// Resolves and validates <see cref="SegmentSelectionInput"/> caller input
/// against SQL Server reference data (Catalog.SegmentDefinitions,
/// Catalog.SegmentOptions) before a Product or Sku Aggregate is asked to
/// assign a Segment (docs task: "Canonical Taxonomy + Segments Domain" §28,
/// §30). Shared by CreateProduct and CreateSku use cases so the validation
/// rules are defined exactly once.
///
/// Validated per input (docs task §28): Definition exists and is Active;
/// AssignmentScope is one of the scopes allowed by the calling use case;
/// each Option exists, belongs to the Definition and is Active; SelectionMode
/// is respected (Single accepts exactly one OptionCode); no duplicated
/// Definition or Option codes across the supplied inputs.
/// </summary>
public sealed class SegmentAssignmentResolver
{
    private readonly ISegmentCatalogRepository _segmentCatalogRepository;

    public SegmentAssignmentResolver(ISegmentCatalogRepository segmentCatalogRepository)
    {
        _segmentCatalogRepository = segmentCatalogRepository;
    }

    public async Task<IReadOnlyCollection<ResolvedSegmentAssignment>> ResolveAsync(
        IReadOnlyCollection<SegmentSelectionInput> inputs,
        IReadOnlyCollection<SegmentAssignmentScope> allowedScopes,
        CancellationToken cancellationToken)
    {
        var results = new List<ResolvedSegmentAssignment>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
            {
                throw new ArgumentException("Segment code cannot be null, empty or whitespace.", nameof(inputs));
            }

            if (!seenCodes.Add(input.Code.Trim()))
            {
                throw new ArgumentException($"Segment '{input.Code}' is duplicated in the request.", nameof(inputs));
            }

            var definition = await _segmentCatalogRepository.GetDefinitionByCodeAsync(input.Code, cancellationToken);

            if (definition is null)
            {
                throw new ArgumentException($"Segment '{input.Code}' does not exist.", nameof(inputs));
            }

            if (!string.Equals(definition.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Segment '{input.Code}' is not active.", nameof(inputs));
            }

            var assignmentScope = ParseAssignmentScope(definition.AssignmentScope);

            if (!allowedScopes.Contains(assignmentScope))
            {
                throw new ArgumentException(
                    $"Segment '{input.Code}' has AssignmentScope '{assignmentScope}', which is not allowed for this operation.",
                    nameof(inputs));
            }

            var selectionMode = ParseSelectionMode(definition.SelectionMode);

            if (input.OptionCodes.Count == 0)
            {
                throw new ArgumentException($"Segment '{input.Code}' requires at least one option code.", nameof(inputs));
            }

            if (selectionMode == SegmentSelectionMode.Single && input.OptionCodes.Count > 1)
            {
                throw new ArgumentException($"Segment '{input.Code}' only accepts a single option code.", nameof(inputs));
            }

            if (input.OptionCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != input.OptionCodes.Count)
            {
                throw new ArgumentException($"Segment '{input.Code}' has duplicated option codes.", nameof(inputs));
            }

            var options = new List<SegmentOptionSelection>();

            foreach (var optionCode in input.OptionCodes)
            {
                var option = await _segmentCatalogRepository.GetOptionAsync(definition.SegmentDefinitionId, optionCode, cancellationToken);

                if (option is null)
                {
                    throw new ArgumentException($"'{optionCode}' is not a valid option for Segment '{input.Code}'.", nameof(inputs));
                }

                if (!string.Equals(option.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Option '{optionCode}' for Segment '{input.Code}' is not active.", nameof(inputs));
                }

                options.Add(new SegmentOptionSelection(new SegmentOptionId(option.SegmentOptionId), option.Code));
            }

            results.Add(new ResolvedSegmentAssignment(
                new SegmentDefinitionId(definition.SegmentDefinitionId),
                definition.Code,
                assignmentScope,
                options));
        }

        return results;
    }

    private static SegmentAssignmentScope ParseAssignmentScope(string value) =>
        Enum.Parse<SegmentAssignmentScope>(value);

    private static SegmentSelectionMode ParseSelectionMode(string value) =>
        Enum.Parse<SegmentSelectionMode>(value);
}
