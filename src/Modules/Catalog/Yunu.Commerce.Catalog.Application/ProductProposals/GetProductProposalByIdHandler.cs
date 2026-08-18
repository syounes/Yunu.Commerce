using Yunu.Commerce.Catalog.Domain.ProductProposals;

namespace Yunu.Commerce.Catalog.Application.ProductProposals;

/// <summary>
/// Orchestrates retrieval of a ProductProposal by identity and maps it to a
/// dedicated read model (docs task: "Catalog intent resolution
/// orchestration" - proposal persistence). Returns null when the proposal
/// does not exist; translation to an HTTP-specific outcome (e.g. 404)
/// belongs to the Host, not to Application.
/// </summary>
public sealed class GetProductProposalByIdHandler
{
    private readonly IProductProposalRepository _proposalRepository;

    public GetProductProposalByIdHandler(IProductProposalRepository proposalRepository)
    {
        _proposalRepository = proposalRepository;
    }

    public async Task<GetProductProposalByIdResult?> HandleAsync(
        GetProductProposalByIdQuery query,
        CancellationToken cancellationToken)
    {
        var proposalId = new ProductProposalId(query.ProposalId);

        var proposal = await _proposalRepository.GetByIdAsync(proposalId, cancellationToken);

        if (proposal is null)
        {
            return null;
        }

        return new GetProductProposalByIdResult(
            proposal.Id.Value,
            proposal.Status.ToString(),
            proposal.Locale,
            new ProposalSourceDto(
                proposal.Source.OriginalInput,
                proposal.Source.NormalizedQuery,
                proposal.Source.SemanticQuery,
                proposal.Source.Intent,
                proposal.Source.DetectedLanguage,
                proposal.Source.TargetLocale),
            new ProposedProductDto(
                proposal.Product.SuggestedName,
                proposal.Product.Description,
                proposal.Product.BrandId,
                new ProposedGoogleCategoryDto(
                    proposal.Product.GoogleCategory.GoogleCategoryId,
                    proposal.Product.GoogleCategory.Name,
                    proposal.Product.GoogleCategory.Path,
                    proposal.Product.GoogleCategory.Depth,
                    proposal.Product.GoogleCategory.ResolutionStrategy,
                    proposal.Product.GoogleCategory.Similarity,
                    proposal.Product.GoogleCategory.RerankConfidence)),
            proposal.Skus.Select(sku => new ProposedSkuDto(
                sku.Id,
                sku.SuggestedCode,
                sku.Gtin,
                sku.Attributes.Select(attribute => new ProposedSkuAttributeDto(
                    attribute.AttributeDefinitionId.Value,
                    attribute.AttributeCode,
                    attribute.AttributeName,
                    attribute.Sequence,
                    attribute.DataType.ToString(),
                    attribute.RawName,
                    attribute.RawValue,
                    attribute.NormalizedValue,
                    attribute.TypedValue is null
                        ? null
                        : new ProposedTypedValueDto(
                            attribute.TypedValue.DisplayValue,
                            attribute.TypedValue.TextValue,
                            attribute.TypedValue.IntegerValue,
                            attribute.TypedValue.DecimalValue,
                            attribute.TypedValue.BooleanValue,
                            attribute.TypedValue.DateTimeValue,
                            attribute.TypedValue.MoneyAmount,
                            attribute.TypedValue.CurrencyCode,
                            attribute.TypedValue.MeasurementValue,
                            attribute.TypedValue.UnitCode,
                            attribute.TypedValue.JsonValue),
                    attribute.AttributeOptionId?.Value,
                    attribute.OptionCode,
                    attribute.OptionName,
                    attribute.DefinitionResolutionStrategy,
                    attribute.OptionResolutionStrategy,
                    attribute.DefinitionSimilarity,
                    attribute.ValueSimilarity,
                    attribute.DefinitionRerankConfidence,
                    attribute.OptionRerankConfidence))
                .ToArray()))
                .ToArray(),
            new ProposalResolutionDto(
                proposal.Resolution.Status,
                proposal.Resolution.CategoryResolved,
                proposal.Resolution.AllAttributesResolved,
                proposal.Resolution.ReadyForProposal,
                proposal.Resolution.IntentConfidence,
                proposal.Resolution.Warnings),
            proposal.CreatedAtUtc,
            proposal.UpdatedAtUtc,
            proposal.ConfirmedAtUtc,
            proposal.ConvertedAtUtc,
            proposal.CreatedProductId?.Value);
    }
}
