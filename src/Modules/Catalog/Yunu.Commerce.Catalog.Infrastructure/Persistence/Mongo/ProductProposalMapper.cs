using Yunu.Commerce.Catalog.Domain.Attributes;
using Yunu.Commerce.Catalog.Domain.ProductProposals;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// Explicit, hand-written mapping between the ProductProposal Aggregate and
/// its MongoDB persistence model (docs task: "Catalog intent resolution
/// orchestration" - proposal persistence). No AutoMapper is used
/// (docs/adr/0001 §9, "prefer explicit mapping"), mirroring <see
/// cref="ProductDocumentMapper"/> and <see cref="SkuDocumentMapper"/>. Never
/// reads or writes ProductProposal.DomainEvents.
/// </summary>
internal static class ProductProposalMapper
{
    public static ProductProposalMongoModel ToMongoModel(ProductProposal proposal)
    {
        return new ProductProposalMongoModel
        {
            Id = proposal.Id.Value,
            Status = proposal.Status.ToString(),
            Locale = proposal.Locale,
            Source = new ProposalSourceMongoModel
            {
                OriginalInput = proposal.Source.OriginalInput,
                NormalizedQuery = proposal.Source.NormalizedQuery,
                SemanticQuery = proposal.Source.SemanticQuery,
                Intent = proposal.Source.Intent,
                DetectedLanguage = proposal.Source.DetectedLanguage,
                TargetLocale = proposal.Source.TargetLocale
            },
            Product = new ProposedProductMongoModel
            {
                SuggestedName = proposal.Product.SuggestedName,
                Description = proposal.Product.Description,
                BrandId = proposal.Product.BrandId,
                FamilyId = proposal.Product.FamilyId,
                GoogleCategory = new ProposedGoogleCategoryMongoModel
                {
                    GoogleCategoryId = proposal.Product.GoogleCategory.GoogleCategoryId,
                    Name = proposal.Product.GoogleCategory.Name,
                    Path = proposal.Product.GoogleCategory.Path,
                    Depth = proposal.Product.GoogleCategory.Depth,
                    ResolutionStrategy = proposal.Product.GoogleCategory.ResolutionStrategy,
                    Similarity = proposal.Product.GoogleCategory.Similarity,
                    RerankConfidence = proposal.Product.GoogleCategory.RerankConfidence
                }
            },
            Skus = proposal.Skus.Select(ToSkuMongoModel).ToList(),
            Resolution = new ProposalResolutionMongoModel
            {
                Status = proposal.Resolution.Status,
                CategoryResolved = proposal.Resolution.CategoryResolved,
                AllAttributesResolved = proposal.Resolution.AllAttributesResolved,
                ReadyForProposal = proposal.Resolution.ReadyForProposal,
                IntentConfidence = proposal.Resolution.IntentConfidence,
                Warnings = proposal.Resolution.Warnings.ToList()
            },
            CreatedAtUtc = proposal.CreatedAtUtc,
            UpdatedAtUtc = proposal.UpdatedAtUtc,
            ConfirmedAtUtc = proposal.ConfirmedAtUtc,
            ConvertedAtUtc = proposal.ConvertedAtUtc,
            CreatedProductId = proposal.CreatedProductId?.Value
        };
    }

    public static ProductProposal ToDomain(ProductProposalMongoModel model)
    {
        var proposal = ProductProposal.Hydrate(
            new ProductProposalId(model.Id),
            Enum.Parse<ProductProposalStatus>(model.Status),
            model.Locale,
            new ProposalSource(
                model.Source.OriginalInput,
                model.Source.NormalizedQuery,
                model.Source.SemanticQuery,
                model.Source.Intent,
                model.Source.DetectedLanguage,
                model.Source.TargetLocale),
            new ProposedProduct(
                model.Product.SuggestedName,
                model.Product.Description,
                model.Product.BrandId,
                model.Product.FamilyId,
                new ProposedGoogleCategory(
                    model.Product.GoogleCategory.GoogleCategoryId,
                    model.Product.GoogleCategory.Name,
                    model.Product.GoogleCategory.Path,
                    model.Product.GoogleCategory.Depth,
                    model.Product.GoogleCategory.ResolutionStrategy,
                    model.Product.GoogleCategory.Similarity,
                    model.Product.GoogleCategory.RerankConfidence)),
            model.Skus.Select(ToSkuDomain),
            new ProposalResolution(
                model.Resolution.Status,
                model.Resolution.CategoryResolved,
                model.Resolution.AllAttributesResolved,
                model.Resolution.ReadyForProposal,
                model.Resolution.IntentConfidence,
                model.Resolution.Warnings),
            model.CreatedAtUtc,
            model.UpdatedAtUtc,
            model.ConfirmedAtUtc,
            model.ConvertedAtUtc,
            model.CreatedProductId is { } createdProductId ? new ProductId(createdProductId) : (ProductId?)null);

        proposal.ClearDomainEvents();

        return proposal;
    }

    private static ProposedSkuMongoModel ToSkuMongoModel(ProposedSku sku)
    {
        return new ProposedSkuMongoModel
        {
            Id = sku.Id,
            SuggestedCode = sku.SuggestedCode,
            Gtin = sku.Gtin,
            Attributes = sku.Attributes.Select(ToAttributeMongoModel).ToList()
        };
    }

    private static ProposedSku ToSkuDomain(ProposedSkuMongoModel model)
    {
        return new ProposedSku(
            model.Id,
            model.SuggestedCode,
            model.Gtin,
            model.Attributes.Select(ToAttributeDomain).ToArray());
    }

    private static ProposedSkuAttributeMongoModel ToAttributeMongoModel(ProposedSkuAttribute attribute)
    {
        return new ProposedSkuAttributeMongoModel
        {
            AttributeDefinitionId = attribute.AttributeDefinitionId.Value,
            AttributeCode = attribute.AttributeCode,
            AttributeName = attribute.AttributeName,
            Sequence = attribute.Sequence,
            DataType = attribute.DataType.ToString(),
            RawName = attribute.RawName,
            RawValue = attribute.RawValue,
            NormalizedValue = attribute.NormalizedValue,
            TypedValue = attribute.TypedValue is null ? null : ToTypedValueMongoModel(attribute.TypedValue),
            AttributeOptionId = attribute.AttributeOptionId?.Value,
            OptionCode = attribute.OptionCode,
            OptionName = attribute.OptionName,
            DefinitionResolutionStrategy = attribute.DefinitionResolutionStrategy,
            OptionResolutionStrategy = attribute.OptionResolutionStrategy,
            DefinitionSimilarity = attribute.DefinitionSimilarity,
            ValueSimilarity = attribute.ValueSimilarity,
            DefinitionRerankConfidence = attribute.DefinitionRerankConfidence,
            OptionRerankConfidence = attribute.OptionRerankConfidence
        };
    }

    private static ProposedSkuAttribute ToAttributeDomain(ProposedSkuAttributeMongoModel model)
    {
        return new ProposedSkuAttribute(
            new AttributeDefinitionId(model.AttributeDefinitionId),
            model.AttributeCode,
            model.AttributeName,
            model.Sequence,
            Enum.Parse<SkuAttributeDataType>(model.DataType),
            model.RawName,
            model.RawValue,
            model.NormalizedValue,
            model.TypedValue is null ? null : ToTypedValueDomain(model.TypedValue),
            model.AttributeOptionId is { } optionId ? new AttributeOptionId(optionId) : (AttributeOptionId?)null,
            model.OptionCode,
            model.OptionName,
            model.DefinitionResolutionStrategy,
            model.OptionResolutionStrategy,
            model.DefinitionSimilarity,
            model.ValueSimilarity,
            model.DefinitionRerankConfidence,
            model.OptionRerankConfidence);
    }

    private static ProposedTypedValueMongoModel ToTypedValueMongoModel(ProposedTypedValue value)
    {
        return new ProposedTypedValueMongoModel
        {
            DisplayValue = value.DisplayValue,
            TextValue = value.TextValue,
            IntegerValue = value.IntegerValue,
            DecimalValue = value.DecimalValue,
            BooleanValue = value.BooleanValue,
            DateTimeValue = value.DateTimeValue,
            MoneyAmount = value.MoneyAmount,
            CurrencyCode = value.CurrencyCode,
            MeasurementValue = value.MeasurementValue,
            UnitCode = value.UnitCode,
            JsonValue = value.JsonValue
        };
    }

    private static ProposedTypedValue ToTypedValueDomain(ProposedTypedValueMongoModel model)
    {
        return new ProposedTypedValue(
            model.DisplayValue,
            model.TextValue,
            model.IntegerValue,
            model.DecimalValue,
            model.BooleanValue,
            model.DateTimeValue,
            model.MoneyAmount,
            model.CurrencyCode,
            model.MeasurementValue,
            model.UnitCode,
            model.JsonValue);
    }
}
