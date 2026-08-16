using Yunu.Commerce.Catalog.Application.AttributeResolution;
using Yunu.Commerce.Catalog.Application.CatalogIntentResolution;
using Yunu.Commerce.Catalog.Application.CategoryResolution;
using Yunu.Commerce.Catalog.Domain.Attributes;
using Yunu.Commerce.Catalog.Domain.ProductProposals;

namespace Yunu.Commerce.Catalog.Application.ProductProposals;

/// <summary>
/// Orchestrates creation of a new <see cref="ProductProposal"/> from
/// natural-language input (docs task: "Catalog intent resolution
/// orchestration" - proposal persistence). Calls <see
/// cref="ICatalogIntentResolutionOrchestrator"/> exactly once and reuses its
/// result as-is: this handler never calls the Intent Rewriter, the Google
/// Category Resolver or the Attribute Hint Resolver directly, and never
/// triggers an additional LLM call. Only maps the already-resolved outcome
/// into the ProductProposal Aggregate and persists it.
///
/// A proposal is persisted only when every readiness criterion is met (see
/// <see cref="IsReadyForPersistence"/>); otherwise a <see
/// cref="ProductProposalResolutionException"/> is thrown carrying the full
/// resolution outcome, and nothing is persisted.
/// </summary>
public sealed class CreateProductProposalHandler
{
    private const string DefaultLocale = "pt-BR";

    private readonly ICatalogIntentResolutionOrchestrator _orchestrator;
    private readonly IProductProposalRepository _proposalRepository;

    public CreateProductProposalHandler(
        ICatalogIntentResolutionOrchestrator orchestrator,
        IProductProposalRepository proposalRepository)
    {
        _orchestrator = orchestrator;
        _proposalRepository = proposalRepository;
    }

    public async Task<CreateProductProposalResult> HandleAsync(
        CreateProductProposalCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Input))
        {
            throw new ArgumentException("Input cannot be null, empty or whitespace.", nameof(command));
        }

        var locale = string.IsNullOrWhiteSpace(command.Locale) ? DefaultLocale : command.Locale;

        var resolution = await _orchestrator.ResolveAsync(
            new CatalogIntentResolutionRequest(command.Input, locale),
            cancellationToken);

        if (!IsReadyForPersistence(resolution))
        {
            throw new ProductProposalResolutionException(resolution);
        }

        var intent = resolution.Intent!;
        var category = resolution.Category!;

        var proposalId = ProductProposalId.New();

        var source = new ProposalSource(
            intent.OriginalInput,
            intent.NormalizedQuery,
            intent.SemanticQuery,
            intent.Intent.ToString(),
            intent.DetectedLanguage,
            intent.TargetLocale);

        var proposedProduct = new ProposedProduct(
            SuggestedName: null,
            Description: null,
            BrandId: null,
            FamilyId: null,
            GoogleCategory: MapGoogleCategory(category));

        var attributes = resolution.Attributes?.Attributes ?? Array.Empty<ResolvedAttributeHint>();
        var proposedAttributes = MapAttributes(attributes);

        var proposedSku = new ProposedSku(
            Id: Guid.NewGuid(),
            SuggestedCode: null,
            Gtin: null,
            Attributes: proposedAttributes);

        var proposalResolution = new ProposalResolution(
            resolution.Status.ToString(),
            CategoryResolved: category.Status == GoogleCategoryResolutionStatus.Resolved,
            AllAttributesResolved: resolution.Attributes is null || resolution.Attributes.AllResolved,
            resolution.ReadyForProposal,
            intent.Confidence,
            resolution.Warnings);

        var proposal = ProductProposal.Create(
            proposalId,
            locale,
            source,
            proposedProduct,
            [proposedSku],
            proposalResolution);

        await _proposalRepository.AddAsync(proposal, cancellationToken);

        return new CreateProductProposalResult(
            proposalId.Value,
            proposal.Status.ToString(),
            resolution.ReadyForProposal,
            proposal.CreatedAtUtc);
    }

    /// <summary>
    /// A proposal may only be persisted when every criterion listed by the
    /// docs task is satisfied: the intent resolved to
    /// <see cref="CatalogIntentResolutionStatus.Resolved"/>, is ready for
    /// proposal, the category is resolved with a non-null GoogleCategoryId,
    /// every attribute hint is resolved (or none were produced), and the
    /// Intent Rewriter classified the input as a product creation intent.
    /// </summary>
    private static bool IsReadyForPersistence(CatalogIntentResolutionResult resolution)
    {
        if (resolution.Status != CatalogIntentResolutionStatus.Resolved)
        {
            return false;
        }

        if (!resolution.ReadyForProposal)
        {
            return false;
        }

        if (resolution.Intent is null || resolution.Intent.Intent != AI.Application.IntentRewriting.CatalogIntent.ProductCreation)
        {
            return false;
        }

        if (resolution.Category is null ||
            resolution.Category.Status != GoogleCategoryResolutionStatus.Resolved ||
            resolution.Category.GoogleCategoryId is null)
        {
            return false;
        }

        if (resolution.Attributes is not null && !resolution.Attributes.AllResolved)
        {
            return false;
        }

        return true;
    }

    private static ProposedGoogleCategory MapGoogleCategory(ResolveGoogleCategoryResult category)
    {
        return new ProposedGoogleCategory(
            category.GoogleCategoryId!.Value,
            category.CategoryName ?? string.Empty,
            category.CategoryPath ?? string.Empty,
            category.Depth ?? 0,
            category.Strategy?.ToString(),
            category.Similarity,
            category.RerankConfidence);
    }

    /// <summary>
    /// Maps every resolved attribute hint to a <see cref="ProposedSkuAttribute"/>,
    /// preserving original order. When the resolver does not supply a
    /// sequence, a deterministic one is generated per AttributeDefinitionId:
    /// starting at 1 and incrementing only when more than one value exists
    /// for the same definition (docs task requirement).
    /// </summary>
    private static IReadOnlyCollection<ProposedSkuAttribute> MapAttributes(
        IReadOnlyList<ResolvedAttributeHint> attributes)
    {
        var sequenceByDefinition = new Dictionary<int, int>();
        var result = new List<ProposedSkuAttribute>(attributes.Count);

        foreach (var attribute in attributes)
        {
            if (attribute.AttributeDefinitionId is null || attribute.DataType is null)
            {
                // Not resolved: nothing to map. Readiness is already validated
                // before this method is called, so this only happens when the
                // caller chose to map a partial result outside the persisted path.
                continue;
            }

            var definitionId = attribute.AttributeDefinitionId.Value;

            var sequence = sequenceByDefinition.TryGetValue(definitionId, out var currentSequence)
                ? currentSequence + 1
                : 1;

            sequenceByDefinition[definitionId] = sequence;

            result.Add(new ProposedSkuAttribute(
                new AttributeDefinitionId(definitionId),
                attribute.AttributeCode ?? string.Empty,
                attribute.AttributeName ?? string.Empty,
                sequence,
                Enum.Parse<SkuAttributeDataType>(attribute.DataType, ignoreCase: true),
                attribute.RawName,
                attribute.RawValue,
                attribute.NormalizedValue,
                MapTypedValue(attribute.TypedValue),
                attribute.AttributeOptionId is { } optionId ? new AttributeOptionId(optionId) : null,
                attribute.OptionCode,
                attribute.OptionName,
                attribute.DefinitionStrategy?.ToString(),
                attribute.OptionStrategy?.ToString(),
                attribute.DefinitionSimilarity,
                attribute.ValueSimilarity,
                attribute.DefinitionRerankConfidence,
                attribute.OptionRerankConfidence));
        }

        return result;
    }

    private static ProposedTypedValue? MapTypedValue(ResolvedAttributeValue? typedValue)
    {
        if (typedValue is null)
        {
            return null;
        }

        return new ProposedTypedValue(
            typedValue.DisplayValue,
            typedValue.TextValue,
            typedValue.IntegerValue,
            typedValue.DecimalValue,
            typedValue.BooleanValue,
            typedValue.DateTimeValue,
            typedValue.MoneyAmount,
            typedValue.CurrencyCode,
            typedValue.MeasurementValue,
            typedValue.UnitCode,
            typedValue.JsonValue);
    }
}
