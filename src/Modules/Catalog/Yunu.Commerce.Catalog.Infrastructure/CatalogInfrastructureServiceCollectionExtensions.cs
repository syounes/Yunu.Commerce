using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Npgsql;
using Yunu.Commerce.Catalog.Application.AttributeCatalog;
using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;
using Yunu.Commerce.Catalog.Application.AttributeResolution;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.EffectiveSegmentDefinitions;
using Yunu.Commerce.Catalog.Application.CategoryResolution;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings;
using Yunu.Commerce.Catalog.Application.SegmentCatalog;
using Yunu.Commerce.Catalog.Application.SegmentDefinitions;
using Yunu.Commerce.Catalog.Application.SegmentEmbeddings;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.ProductProposals;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Segments;
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
using Yunu.Commerce.Catalog.Infrastructure.SegmentEmbeddings.PostgreSql;
using Yunu.Commerce.Catalog.Infrastructure.SegmentEmbeddings.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.SegmentEmbeddings.Synchronization.InMemory;
using Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.Synchronization.InMemory;

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
        services.AddSingleton<Yunu.Commerce.Catalog.Domain.Concurrency.IProductSkuConcurrencyCoordinator, MongoProductSkuConcurrencyCoordinator>();

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
        services.AddSingleton<ICanonicalTaxonomySegmentAssociationReader, SqlCanonicalTaxonomySegmentAssociationReader>();
        services.AddSingleton<Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.ICanonicalTaxonomyNodeUsageReader, SqlCanonicalTaxonomyNodeUsageReader>();
        services.AddSingleton<ISegmentCatalogRepository, SqlSegmentCatalogRepository>();
        services.AddSingleton<ISegmentDefinitionRepository, SqlSegmentDefinitionRepository>();
        services.AddSingleton<ISegmentOptionRepository, SqlSegmentOptionRepository>();
        services.AddSingleton<ISegmentDefinitionUsageReader, SqlSegmentDefinitionUsageReader>();

        services.AddSingleton<ISegmentEmbeddingSourceRepository, SqlSegmentEmbeddingSourceRepository>();
        services.AddSingleton<ISegmentEmbeddingRepository, PostgreSqlSegmentEmbeddingRepository>();
        services.Configure<SegmentEmbeddingsSyncOptions>(configuration.GetSection("Catalog:SegmentEmbeddings"));
        services.AddSingleton<ISegmentEmbeddingSynchronizationGuard, InMemorySegmentEmbeddingSynchronizationGuard>();

        // SourceTaxonomy is provider-neutral imported/reference data (docs/adr/0014).
        // SqlSourceTaxonomyRepository depends only on a plain connection string, never
        // on GoogleTaxonomySqlOptions directly. The legacy naming debt of reusing the
        // Google-named options type as the shared Catalog SQL connection source is
        // isolated to this composition root rather than leaking into SourceTaxonomy
        // Infrastructure.
        services.AddSingleton<ISourceTaxonomyRepository>(sp =>
        {
            var connectionString = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GoogleTaxonomySqlOptions>>().Value.ConnectionString;
            return new SqlSourceTaxonomyRepository(connectionString);
        });

        // SourceTaxonomy Phase 3: generic import orchestration infrastructure
        // (docs/adr/0014-provider-neutral-source-taxonomy.md §9-§18). No concrete
        // ISourceTaxonomyAdapter is registered here; adapters are provider-specific
        // and belong to a later phase.
        services.AddSingleton<ISourceTaxonomyImportStore>(sp =>
        {
            var connectionString = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GoogleTaxonomySqlOptions>>().Value.ConnectionString;
            return new SqlSourceTaxonomyImportStore(connectionString);
        });
        services.AddSingleton<ISourceTaxonomySynchronizationStore>(sp =>
        {
            var connectionString = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GoogleTaxonomySqlOptions>>().Value.ConnectionString;
            return new SqlSourceTaxonomySynchronizationStore(connectionString);
        });
        services.AddSingleton<ISourceTaxonomyImportGuard, InMemorySourceTaxonomyImportGuard>();

        return services;
    }
}
