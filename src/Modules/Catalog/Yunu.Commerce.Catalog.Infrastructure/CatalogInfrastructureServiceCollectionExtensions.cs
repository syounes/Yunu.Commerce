using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Npgsql;
using Yunu.Commerce.Catalog.Application.AttributeCatalog;
using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;
using Yunu.Commerce.Catalog.Application.AttributeResolution;
using Yunu.Commerce.Catalog.Application.CategoryResolution;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings;
using Yunu.Commerce.Catalog.Application.SegmentCatalog;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.ProductProposals;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;
using Yunu.Commerce.Catalog.Infrastructure.AttributeCatalog.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.AttributeEmbeddings.PostgreSql;
using Yunu.Commerce.Catalog.Infrastructure.AttributeEmbeddings.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.AttributeEmbeddings.Synchronization.InMemory;
using Yunu.Commerce.Catalog.Infrastructure.Brands.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.CanonicalTaxonomy.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.CategoryResolution.PostgreSql;
using Yunu.Commerce.Catalog.Infrastructure.CategoryResolution.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Embeddings.PostgreSql;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Sources.Http;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Synchronization.InMemory;
using Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;
using Yunu.Commerce.Catalog.Infrastructure.SegmentCatalog.SqlServer;

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
        services.AddSingleton<IProductProposalRepository, MongoProductProposalRepository>();

        services.Configure<GoogleTaxonomyOptions>(configuration.GetSection("Catalog:GoogleTaxonomy"));
        services.Configure<GoogleTaxonomySqlOptions>(configuration.GetSection("Catalog:GoogleTaxonomySql"));

        services.AddHttpClient(GoogleTaxonomyHttpSource.HttpClientName, httpClient =>
        {
            httpClient.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<IGoogleTaxonomySource, GoogleTaxonomyHttpSource>();
        services.AddSingleton<IGoogleTaxonomyRepository, SqlGoogleTaxonomyRepository>();
        services.AddSingleton<IGoogleTaxonomySynchronizationGuard, InMemoryGoogleTaxonomySynchronizationGuard>();
        services.AddSingleton<IAttributeCatalogRepository, SqlAttributeCatalogRepository>();

        services.AddSingleton(sp =>
        {
            var connectionString = configuration.GetConnectionString("VectorStore");
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            return dataSourceBuilder.Build();
        });

        services.AddSingleton<IGoogleTaxonomyEmbeddingRepository, PostgreSqlGoogleTaxonomyEmbeddingRepository>();

        services.Configure<GoogleTaxonomyEmbeddingsSyncOptions>(configuration.GetSection("Catalog:GoogleTaxonomyEmbeddings"));
        services.AddSingleton<IGoogleTaxonomyEmbeddingSynchronizationGuard, InMemoryGoogleTaxonomyEmbeddingSynchronizationGuard>();

        services.AddSingleton<IAttributeEmbeddingSourceRepository, SqlAttributeEmbeddingSourceRepository>();
        services.AddSingleton<IAttributeEmbeddingRepository, PostgreSqlAttributeEmbeddingRepository>();
        services.Configure<AttributeEmbeddingsSyncOptions>(configuration.GetSection("Catalog:AttributeEmbeddings"));
        services.AddSingleton<IAttributeEmbeddingSynchronizationGuard, InMemoryAttributeEmbeddingSynchronizationGuard>();

        services.AddSingleton<IAttributeCatalogReader, SqlAttributeCatalogReader>();
        services.AddSingleton<IAttributeSemanticSearch, PostgreSqlAttributeSemanticSearch>();

        services.AddSingleton<IGoogleCategoryCatalogReader, SqlGoogleCategoryCatalogReader>();
        services.AddSingleton<IGoogleCategorySemanticSearch, PostgreSqlGoogleCategorySemanticSearch>();
        services.AddSingleton<IBrandRepository, SqlBrandRepository>();

        services.AddSingleton<ICanonicalTaxonomyRepository, SqlCanonicalTaxonomyRepository>();
        services.AddSingleton<ISegmentCatalogRepository, SqlSegmentCatalogRepository>();

        return services;
    }
}
