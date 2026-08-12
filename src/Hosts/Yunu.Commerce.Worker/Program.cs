using Yunu.Commerce.Observability;
using Yunu.Commerce.Worker;
using Yunu.Commerce.Catalog.Application;
using Yunu.Commerce.Catalog.Infrastructure;
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

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddYunuObservability("Yunu.Commerce.Worker");

builder.Services
    .AddCatalogApplication()
    .AddSellersApplication()
    .AddOffersApplication()
    .AddPricingApplication()
    .AddAvailabilityApplication()
    .AddFulfillmentApplication()
    .AddFreightApplication()
    .AddSearchApplication()
    .AddAIApplication()
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

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();
