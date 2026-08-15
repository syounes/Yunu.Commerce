using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;
using Yunu.Commerce.Catalog.Application.AttributeResolution;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings;
using Yunu.Commerce.Catalog.Application.Products.CreateProduct;
using Yunu.Commerce.Catalog.Application.Products.GetProductById;
using Yunu.Commerce.Catalog.Application.Skus.CreateSku;
using Yunu.Commerce.Catalog.Application.Skus.GetSkuById;
using Yunu.Commerce.Catalog.Application.Skus.GetSkusByProductId;

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

        services.AddScoped<SynchronizeGoogleTaxonomyHandler>();
        services.AddScoped<GenerateGoogleTaxonomyEmbeddingHandler>();
        services.AddScoped<SynchronizeGoogleTaxonomyEmbeddingsHandler>();
        services.AddScoped<SynchronizeAttributeEmbeddingsHandler>();

        services.AddOptions<AttributeResolutionOptions>()
            .Bind(configuration.GetSection("AI:AttributeResolution"))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AttributeResolutionOptions>, AttributeResolutionOptionsValidator>();

        services.AddScoped<IAttributeHintResolver, AttributeHintResolver>();

        return services;
    }
}
