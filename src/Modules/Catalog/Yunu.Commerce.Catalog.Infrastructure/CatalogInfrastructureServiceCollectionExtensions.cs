using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Products.Skus;
using Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

namespace Yunu.Commerce.Catalog.Infrastructure;

/// <summary>
/// Composition entry point for the Catalog Infrastructure layer (docs §49).
/// Registers the MongoDB adapters implementing IProductRepository and ISkuRepository
/// (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// No Outbox, Kafka, Redis, Elasticsearch or GenAI adapter is registered yet.
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

        return services;
    }
}
