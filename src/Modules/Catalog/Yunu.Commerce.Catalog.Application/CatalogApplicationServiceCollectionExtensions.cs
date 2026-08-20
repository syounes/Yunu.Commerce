using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;
using Yunu.Commerce.Catalog.Application.AttributeResolution;
using Yunu.Commerce.Catalog.Application.CatalogIntentResolution;
using Yunu.Commerce.Catalog.Application.CategoryResolution;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings;
using Yunu.Commerce.Catalog.Application.Products.CreateProduct;
using Yunu.Commerce.Catalog.Application.Products.GetProductById;
using Yunu.Commerce.Catalog.Application.ProductProposals;
using Yunu.Commerce.Catalog.Application.Skus.CreateSku;
using Yunu.Commerce.Catalog.Application.Skus.GetSkuById;
using Yunu.Commerce.Catalog.Application.Skus.GetSkusByProductId;
using Yunu.Commerce.Catalog.Application.Brands.CreateBrand;
using Yunu.Commerce.Catalog.Application.Brands.DeleteBrand;
using Yunu.Commerce.Catalog.Application.Brands.GetBrand;
using Yunu.Commerce.Catalog.Application.Brands.UpdateBrand;
using Yunu.Commerce.Catalog.Application.Brands.ResolveBrand;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.CreateCanonicalTaxonomyNode;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.EffectiveSegmentDefinitions;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.GetCanonicalTaxonomyChildren;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.GetCanonicalTaxonomyNodeById;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.GetCanonicalTaxonomyRoots;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.UpdateCanonicalTaxonomyNode;
using Yunu.Commerce.Catalog.Application.SegmentCatalog;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentDefinitionByCode;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentDefinitionById;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentDefinitions;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionByCode;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionById;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionsByDefinition;
using Yunu.Commerce.Catalog.Application.SegmentDefinitions.CreateSegmentDefinition;
using Yunu.Commerce.Catalog.Application.SegmentDefinitions.UpdateSegmentDefinition;
using Yunu.Commerce.Catalog.Application.SegmentOptions.CreateSegmentOption;
using Yunu.Commerce.Catalog.Application.SegmentOptions.UpdateSegmentOption;
using Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

namespace Yunu.Commerce.Catalog.Application;

/// <summary>
/// Composition entry point for the Catalog Application layer (docs §49).
/// Hosts call this extension to register use case handlers and application services.
/// Only registrations actually required by the currently implemented handlers are added.
/// </summary>
public static class CatalogApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<CreateProductHandler>();
        services.AddScoped<GetProductByIdHandler>();
        services.AddScoped<CreateSkuHandler>();
        services.AddScoped<GetSkuByIdHandler>();
        services.AddScoped<GetSkusByProductIdHandler>();

        services.AddScoped<SegmentAssignmentResolver>();

        services.AddScoped<CreateBrandHandler>();
        services.AddScoped<GetBrandHandler>();
        services.AddScoped<UpdateBrandHandler>();
        services.AddScoped<DeleteBrandHandler>();
        services.AddScoped<Yunu.Commerce.Catalog.Application.Brands.ResolveBrand.IBrandResolver, Yunu.Commerce.Catalog.Application.Brands.ResolveBrand.BrandResolver>();

        services.AddScoped<CreateCanonicalTaxonomyNodeHandler>();
        services.AddScoped<GetCanonicalTaxonomyNodeByIdHandler>();
        services.AddScoped<GetCanonicalTaxonomyChildrenHandler>();
        services.AddScoped<GetCanonicalTaxonomyRootsHandler>();
        services.AddScoped<UpdateCanonicalTaxonomyNodeHandler>();
        services.AddScoped<GetEffectiveSegmentDefinitionsHandler>();

        services.AddScoped<GetSegmentDefinitionsHandler>();
        services.AddScoped<GetSegmentDefinitionByIdHandler>();
        services.AddScoped<GetSegmentDefinitionByCodeHandler>();
        services.AddScoped<GetSegmentOptionsByDefinitionHandler>();
        services.AddScoped<GetSegmentOptionByIdHandler>();
        services.AddScoped<GetSegmentOptionByCodeHandler>();
        services.AddScoped<CreateSegmentDefinitionHandler>();
        services.AddScoped<UpdateSegmentDefinitionHandler>();
        services.AddScoped<CreateSegmentOptionHandler>();
        services.AddScoped<UpdateSegmentOptionHandler>();

        services.AddScoped<CreateProductProposalHandler>();
        services.AddScoped<GetProductProposalByIdHandler>();

        services.AddScoped<SynchronizeGoogleTaxonomyHandler>();
        services.AddScoped<GenerateGoogleTaxonomyEmbeddingHandler>();
        services.AddScoped<SynchronizeGoogleTaxonomyEmbeddingsHandler>();
        services.AddScoped<SynchronizeAttributeEmbeddingsHandler>();
        services.AddScoped<SynchronizeSegmentEmbeddingsHandler>();

        services.AddOptions<AttributeResolutionOptions>()
            .Bind(configuration.GetSection("AI:AttributeResolution"))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AttributeResolutionOptions>, AttributeResolutionOptionsValidator>();

        services.AddScoped<IAttributeHintResolver, AttributeHintResolver>();

        services.AddOptions<CategoryResolutionOptions>()
            .Bind(configuration.GetSection("AI:CategoryResolution"))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<CategoryResolutionOptions>, CategoryResolutionOptionsValidator>();

        services.AddScoped<IGoogleCategoryResolver, GoogleCategoryResolver>();
        services.AddScoped<ICatalogIntentResolutionOrchestrator, CatalogIntentResolutionOrchestrator>();

        return services;
    }
}
