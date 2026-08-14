using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Sources.Http;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Synchronization.InMemory;
using Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

namespace Yunu.Commerce.Catalog.Infrastructure;

/// <summary>
/// Composition entry point for the Catalog Infrastructure layer (docs §49).
/// Registers the MongoDB adapters implementing IProductRepository and ISkuRepository
/// (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md), plus the SQL
/// Server/HTTP adapters for the Google Product Taxonomy import/synchronization
/// feature. No Outbox, Kafka, Redis, Elasticsearch or GenAI adapter is registered yet.
/// </summary>
public static class CatalogInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CatalogMongoOptions>(configuration.GetSection("Catalog:Mongo"));

        services.AddSingleton<IMongoClient>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CatalogMongoOptions>>().Value;
            return new MongoClient(options.ConnectionString);
        });

        services.AddSingleton<IProductRepository, MongoProductRepository>();
        services.AddSingleton<ISkuRepository, MongoSkuRepository>();

        services.Configure<GoogleTaxonomyOptions>(configuration.GetSection("Catalog:GoogleTaxonomy"));
        services.Configure<GoogleTaxonomySqlOptions>(configuration.GetSection("Catalog:GoogleTaxonomySql"));

        services.AddHttpClient(GoogleTaxonomyHttpSource.HttpClientName, httpClient =>
        {
            httpClient.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<IGoogleTaxonomySource, GoogleTaxonomyHttpSource>();
        services.AddSingleton<IGoogleTaxonomyRepository, SqlGoogleTaxonomyRepository>();
        services.AddSingleton<IGoogleTaxonomySynchronizationGuard, InMemoryGoogleTaxonomySynchronizationGuard>();

        return services;
    }
}
