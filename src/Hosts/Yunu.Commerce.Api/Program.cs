using Yunu.Commerce.Observability;
using Yunu.Commerce.Catalog.Application;
using Yunu.Commerce.Catalog.Infrastructure;
using Yunu.Commerce.Api.Products;
using Yunu.Commerce.Api.Skus;
using Yunu.Commerce.Api.GoogleTaxonomy;
using Yunu.Commerce.Api.AttributeEmbeddings;
using Yunu.Commerce.Api.AI;
using Yunu.Commerce.Api.AI.IntentRewriting;
using Yunu.Commerce.Api.AI.AttributeResolution;
using Yunu.Commerce.Api.AI.CategoryResolution;
using Yunu.Commerce.Api.AI.CatalogIntentResolution;
using Yunu.Commerce.Sellers.Application;
using Yunu.Commerce.Sellers.Infrastructure;
using Yunu.Commerce.Offers.Application;
using Yunu.Commerce.Offers.Infrastructure;
using Yunu.Commerce.Pricing.Application;
using Yunu.Commerce.Pricing.Infrastructure;
using Yunu.Commerce.Availability.Application;
using Yunu.Commerce.Availability.Infrastructure;
using Yunu.Commerce.Fulfillment.Application;
using Yunu.Commerce.Fulfillment.Infrastructure;
using Yunu.Commerce.Freight.Application;
using Yunu.Commerce.Freight.Infrastructure;
using Yunu.Commerce.Search.Application;
using Yunu.Commerce.Search.Infrastructure;
using Yunu.Commerce.AI.Application;
using Yunu.Commerce.AI.Infrastructure;
using Yunu.Commerce.Integrations.Application;
using Yunu.Commerce.Integrations.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddYunuObservability("Yunu.Commerce.Api");

builder.Services
    .AddCatalogApplication(builder.Configuration)
    .AddSellersApplication()
    .AddOffersApplication()
    .AddPricingApplication()
    .AddAvailabilityApplication()
    .AddFulfillmentApplication()
    .AddFreightApplication()
    .AddSearchApplication()
    .AddAIApplication(builder.Configuration)
    .AddIntegrationsApplication();

builder.Services
    .AddCatalogInfrastructure(builder.Configuration)
    .AddSellersInfrastructure(builder.Configuration)
    .AddOffersInfrastructure(builder.Configuration)
    .AddPricingInfrastructure(builder.Configuration)
    .AddAvailabilityInfrastructure(builder.Configuration)
    .AddFulfillmentInfrastructure(builder.Configuration)
    .AddFreightInfrastructure(builder.Configuration)
    .AddSearchInfrastructure(builder.Configuration)
    .AddAIInfrastructure(builder.Configuration)
    .AddIntegrationsInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapGet("/", () => Results.Ok(new { service = "Yunu.Commerce.Api", status = "running" }));

app.MapCatalogProductEndpoints();
app.MapCatalogSkuEndpoints();
app.MapCatalogGoogleTaxonomyEndpoints();
app.MapCatalogAttributeEmbeddingEndpoints();
app.MapAIEmbeddingsEndpoints();
app.MapIntentRewritingEndpoints();
app.MapResolveAttributeHintsEndpoints();
app.MapResolveGoogleCategoryEndpoints();
app.MapCatalogIntentResolutionEndpoints();

app.Run();
