# Yunu.Commerce - Hexagonal Architecture

## 1. Purpose

This document defines how Yunu.Commerce applies Hexagonal Architecture, also known as Ports and Adapters Architecture.

The objective is to isolate business capabilities from external technologies.

The core system must communicate with the outside world through explicit ports.

External technologies must interact with the core through adapters.

The central rule is:

> The business core defines what it needs. Infrastructure decides how to provide it.

---

# 2. Architectural Model

The conceptual structure is:

```text
                 Inbound Adapters

        REST API
        Worker
        Kafka Consumer
        CLI
        Scheduled Job
        AI Tool Call
              │
              ▼

        ┌──────────────────┐
        │   Inbound Ports  │
        └────────┬─────────┘
                 │
                 ▼
        ┌──────────────────┐
        │   Application    │
        │     + Domain     │
        └────────┬─────────┘
                 │
                 ▼
        ┌──────────────────┐
        │  Outbound Ports  │
        └────────┬─────────┘
                 │
                 ▼

               Adapters

        MongoDB
        SQL
        Kafka
        Redis
        Elasticsearch
        Azure OpenAI
        Google Vertex AI
        ERP
        Freight Provider
        Object Storage
```

The core must not depend on concrete adapters.

Adapters depend on core abstractions.

---

# 3. Inbound and Outbound Directions

Hexagonal Architecture distinguishes two interaction directions.

## Inbound

Inbound interactions initiate a use case.

Examples:

```text
HTTP Request
Kafka Message
Scheduled Job
CLI Command
AI Agent Tool Call
```

These interactions enter the system through inbound adapters.

---

## Outbound

Outbound interactions are technical capabilities required by the application.

Examples:

```text
Persist aggregate
Publish event
Read cache
Index search document
Call AI model
Call external ERP
Calculate freight using provider
Store object
```

These interactions leave the core through outbound ports.

---

# 4. Inbound Adapters

Inbound adapters translate external invocation mechanisms into application use cases.

Examples include:

```text
ASP.NET Core API
Kafka Consumer Worker
Background Worker
Scheduled Processor
Command Line Tool
AI Tool Endpoint
```

Inbound adapters do not own business rules.

Their responsibility is to:

* receive input
* authenticate where required
* validate transport-level structure
* translate input
* invoke the Application layer
* translate results back to the caller

---

# 5. Inbound Port Concept

An inbound port represents a use-case entry point.

In Yunu.Commerce, Application Commands and Queries usually serve as inbound ports.

Example:

```text
HTTP POST /products
        │
        ▼
CreateProductRequest
        │
        ▼
CreateProductCommand
        │
        ▼
CreateProductHandler
```

The Application use case is independent from HTTP.

The same command may theoretically be initiated from another adapter.

---

# 6. Outbound Ports

Outbound ports define capabilities required by Application or Domain behavior.

Examples:

```text
Persistence
Messaging
Caching
Search
AI
External integrations
Object storage
Clock
Distributed locking
```

Ports must be defined in terms of internal needs.

They must not expose vendor-specific types.

---

# 7. Persistence Ports

Example:

```csharp
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(
        ProductId id,
        CancellationToken cancellationToken);

    Task AddAsync(
        Product product,
        CancellationToken cancellationToken);
}
```

Infrastructure may implement:

```text
MongoProductRepository
SqlProductRepository
InMemoryProductRepository
```

The core does not care which implementation is selected.

---

# 8. Messaging Ports

Example:

```csharp
public interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken);
}
```

Possible implementation:

```text
KafkaIntegrationEventPublisher
```

Future implementation could be:

```text
ServiceBusIntegrationEventPublisher
RabbitMqIntegrationEventPublisher
GooglePubSubEventPublisher
```

Changing the broker must not require changing business logic.

---

# 9. Cache Ports

Example:

```csharp
public interface ICacheProvider
{
    Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken);

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        string key,
        CancellationToken cancellationToken);
}
```

Initial adapter:

```text
RedisCacheProvider
```

Redis-specific types must not leak outside Infrastructure.

---

# 10. Search Ports

Example:

```csharp
public interface IProductIndexer
{
    Task IndexAsync(
        ProductSearchDocument document,
        CancellationToken cancellationToken);
}
```

and:

```csharp
public interface IProductSearch
{
    Task<ProductSearchResult> SearchAsync(
        ProductSearchCriteria criteria,
        CancellationToken cancellationToken);
}
```

Initial implementation:

```text
ElasticsearchProductIndexer
ElasticsearchProductSearch
```

Application must not depend on Elasticsearch query objects.

---

# 11. AI Ports

AI provider abstractions must be provider-independent.

Example:

```csharp
public interface IGenerativeAIProvider
{
    Task<GenerativeAIResponse> GenerateAsync(
        GenerativeAIRequest request,
        CancellationToken cancellationToken);
}
```

Possible adapters:

```text
AzureOpenAIGenerativeAIProvider
GoogleVertexAIGenerativeAIProvider
```

The Application must not know which provider is active.

---

# 12. Embedding Ports

Example:

```csharp
public interface IEmbeddingProvider
{
    Task<EmbeddingVector> GenerateAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken);
}
```

Possible adapters:

```text
AzureOpenAIEmbeddingProvider
GoogleVertexAIEmbeddingProvider
```

Vector representation exposed to Application must remain provider-neutral.

---

# 13. AI Model Router

AI provider selection may be centralized behind a router abstraction.

Example:

```csharp
public interface IAIModelRouter
{
    Task<GenerativeAIResponse> ExecuteAsync(
        GenerativeAIRequest request,
        CancellationToken cancellationToken);
}
```

The router may decide based on:

* operation type
* model capability
* cost
* latency
* availability
* fallback policy
* configuration
* tenant policy

The router is an Application or AI orchestration concern.

Provider adapters remain Infrastructure concerns.

---

# 14. External Integration Ports

External systems must be represented through canonical ports.

Example:

```csharp
public interface IExternalCatalogProvider
{
    Task<ExternalCatalogBatch> ReadProductsAsync(
        ExternalCatalogRequest request,
        CancellationToken cancellationToken);
}
```

Concrete implementations may include:

```text
SapCatalogAdapter
VtexCatalogAdapter
MarketplaceCatalogAdapter
LegacyDatabaseCatalogAdapter
```

External models must be translated before entering the core.

---

# 15. Freight Ports

Freight integrations are a natural use case for Ports & Adapters.

Example:

```csharp
public interface IFreightProvider
{
    Task<FreightProviderResult> CalculateAsync(
        FreightProviderRequest request,
        CancellationToken cancellationToken);
}
```

Possible adapters:

```text
CarrierAFreightAdapter
CarrierBFreightAdapter
MarketplaceFreightAdapter
InternalLogisticsAdapter
```

The Freight Domain must not depend on any carrier SDK.

---

# 16. Object Storage Ports

Example:

```csharp
public interface IObjectStorage
{
    Task<ObjectReference> StoreAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken);
}
```

Adapters may include:

```text
AzureBlobStorageAdapter
GoogleCloudStorageAdapter
LocalFileStorageAdapter
```

Cloud-specific types must remain outside Application.

---

# 17. Time Port

Time can also be modeled as a port when business behavior depends on the current instant.

Example:

```csharp
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
```

Possible adapters:

```text
SystemClock
TestClock
```

This improves deterministic testing.

---

# 18. Distributed Lock Port

When distributed coordination is required, use an abstraction.

Example:

```csharp
public interface IDistributedLock
{
    Task<IAsyncDisposable?> AcquireAsync(
        string resource,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
```

Possible adapter:

```text
RedisDistributedLock
```

The core must not depend directly on Redis locking primitives.

---

# 19. Anti-Corruption Layer

An Anti-Corruption Layer protects Yunu.Commerce from foreign models and terminology.

External systems may have concepts that do not match Yunu.Commerce.

Example:

```text
SAP

MATNR
WERKS
VKORG
```

These must not become internal names.

Instead:

```text
SAP DTO
   │
   ▼
SAP Translator
   │
   ▼
Canonical Integration Model
   │
   ▼
Catalog Application
```

The translation boundary is explicit.

---

# 20. External DTO Isolation

External DTOs must remain in integration or infrastructure code.

Forbidden:

```text
Catalog.Domain.Product
contains SAP MATNR
```

Forbidden:

```text
Pricing.Application
accepts ExternalVendorPriceResponse directly
```

Preferred:

```text
ExternalVendorPriceResponse
        │
        ▼
VendorPriceTranslator
        │
        ▼
Canonical Price Input
```

---

# 21. Canonical Contracts

Yunu.Commerce owns its integration contracts.

Examples:

```text
CanonicalProductInput
CanonicalSkuInput
CanonicalSellerInput
CanonicalOfferInput
CanonicalPriceInput
CanonicalAvailabilityInput
```

These contracts represent Yunu.Commerce semantics.

They must not mirror one external provider exactly.

---

# 22. Adapter Categories

Adapters can be grouped into several categories.

## Persistence Adapters

```text
MongoProductRepository
SqlSellerRepository
SqlPriceRepository
```

## Messaging Adapters

```text
KafkaEventPublisher
KafkaConsumerAdapter
```

## Cache Adapters

```text
RedisCacheProvider
```

## Search Adapters

```text
ElasticsearchProductIndexer
ElasticsearchProductSearch
```

## AI Adapters

```text
AzureOpenAIProvider
GoogleVertexAIProvider
```

## Enterprise Integration Adapters

```text
SapAdapter
VtexAdapter
MarketplaceAdapter
```

## Logistics Adapters

```text
CarrierAdapter
ExternalFreightAdapter
```

## Storage Adapters

```text
AzureBlobStorageAdapter
GoogleCloudStorageAdapter
```

---

# 23. Adapter Ownership

Adapters should live close to the Bounded Context they serve.

Example:

```text
Catalog
├── Domain
├── Application
└── Infrastructure
    └── Persistence
        └── MongoProductRepository
```

AI provider adapters belong to the AI Infrastructure boundary.

Freight provider adapters belong to the Freight or Integration Infrastructure boundary depending on their responsibility.

Avoid one giant global Infrastructure project.

---

# 24. Cross-Context Adapters

A Bounded Context must not use another context's Infrastructure implementation as an adapter.

Forbidden:

```text
Pricing.Application
    │
    ▼
Catalog.Infrastructure.MongoProductRepository
```

If Pricing requires Catalog information, use:

```text
Catalog Application API
Integration Event
Projection
Explicit Contract
```

Context boundaries remain intact.

---

# 25. Inbound HTTP Adapter

Example:

```text
HTTP Client
   │
   ▼
Catalog API
   │
   ▼
CreateProductRequest
   │
   ▼
CreateProductCommand
   │
   ▼
Application
```

The controller or endpoint adapts HTTP into Application language.

HTTP-specific concepts stop at the adapter boundary.

---

# 26. Inbound Kafka Adapter

Kafka consumers are inbound adapters.

Example:

```text
Kafka
  │
  ▼
ProductImportedIntegrationEvent
  │
  ▼
Kafka Consumer Adapter
  │
  ▼
ImportProductCommand
  │
  ▼
Catalog Application
```

Kafka-specific metadata should be translated into internal metadata where required.

---

# 27. Outbound Kafka Adapter

Example:

```text
Catalog Application
      │
      ▼
IIntegrationEventPublisher
      │
      ▼
KafkaIntegrationEventPublisher
      │
      ▼
Kafka
```

The Application must not know:

```text
ProducerConfig
TopicPartition
DeliveryResult
ProducerBuilder
```

---

# 28. Outbound MongoDB Adapter

Example:

```text
Catalog Application
      │
      ▼
IProductRepository
      │
      ▼
MongoProductRepository
      │
      ▼
ProductDocument
      │
      ▼
MongoDB
```

The Domain Aggregate remains independent from MongoDB serialization concerns.

---

# 29. Outbound SQL Adapter

Example:

```text
Pricing Application
      │
      ▼
IPriceRepository
      │
      ▼
SqlPriceRepository
      │
      ▼
SQL Database
```

ORM choice belongs to Infrastructure.

Possible implementations may use:

```text
Entity Framework Core
Dapper
ADO.NET
```

The core must not be redesigned based on ORM convenience.

---

# 30. Outbound Redis Adapter

Example:

```text
Availability Application
      │
      ▼
ICacheProvider
      │
      ▼
RedisCacheProvider
      │
      ▼
Redis
```

Redis is an optimization mechanism, not automatically the business owner of the data.

---

# 31. Outbound Elasticsearch Adapter

Example:

```text
Search Application
      │
      ▼
IProductIndexer
      │
      ▼
ElasticsearchProductIndexer
      │
      ▼
Elasticsearch
```

Search query DSL must remain inside Infrastructure.

---

# 32. Outbound AI Adapter

Example:

```text
AI Application
      │
      ▼
IGenerativeAIProvider
      │
      ├──────── AzureOpenAIProvider
      │
      └──────── GoogleVertexAIProvider
```

Provider configuration stays outside Domain and Application business logic.

---

# 33. Provider Switching

Provider switching must occur through dependency injection and configuration.

Conceptual configuration:

```text
AIProvider = Azure
```

or:

```text
AIProvider = Google
```

The Application should remain unchanged.

---

# 34. Provider Fallback

Fallback may be implemented behind the AI router.

Example:

```text
AI Application
      │
      ▼
AIModelRouter
      │
      ├── Azure OpenAI
      │        │
      │        └── failure
      │
      ▼
Google Vertex AI
```

Fallback logic must be observable.

It must not hide repeated provider failures silently.

---

# 35. Port Granularity

Ports should represent meaningful capabilities.

Avoid giant interfaces such as:

```text
IInfrastructureService
```

with dozens of unrelated operations.

Prefer focused ports:

```text
IProductRepository
IProductSearch
IEventPublisher
ICacheProvider
IGenerativeAIProvider
```

Interfaces should remain cohesive.

---

# 36. Avoid Interface Explosion

Not every class requires an interface.

Interfaces are appropriate when they represent:

* replaceable technology
* external capability
* meaningful boundary
* testable dependency
* multiple valid implementations

Do not create:

```text
IProductNameFormatter
```

just because every class must allegedly have an interface.

Ports exist to protect architecture, not inflate file counts.

---

# 37. Domain Port vs Application Port

Some ports represent Domain persistence needs.

Example:

```text
IProductRepository
```

Others represent Application technical capabilities.

Example:

```text
IIntegrationEventPublisher
IGenerativeAIProvider
IProductIndexer
```

The port should live in the innermost layer that genuinely needs it.

Do not place every interface into a generic shared project.

---

# 38. Dependency Inversion

Dependency inversion means the core defines the abstraction.

Example:

Wrong:

```text
Application
   │
   ▼
MongoProductRepository
```

Correct:

```text
Application / Domain
   │
   ▼
IProductRepository
   ▲
   │
MongoProductRepository
```

The implementation depends on the abstraction.

---

# 39. Adapter Registration

Adapters are selected at the composition root.

Example:

```csharp
services.AddScoped<IProductRepository, MongoProductRepository>();
```

or:

```csharp
services.AddSingleton<IGenerativeAIProvider, AzureOpenAIProvider>();
```

Registration code belongs to Infrastructure or Host composition.

---

# 40. Environment-Specific Adapters

Different environments may use different adapters.

Example:

```text
Development
    InMemoryObjectStorage

Integration Tests
    Testcontainers MongoDB

Production
    Azure Blob Storage
```

The Application remains unchanged.

---

# 41. Testing Ports

Ports allow lightweight test substitutes.

Example:

```text
IClock
    ├── SystemClock
    └── TestClock

IGenerativeAIProvider
    ├── AzureOpenAIProvider
    └── FakeGenerativeAIProvider
```

Domain and Application tests should not require external cloud providers.

---

# 42. Contract Tests for Adapters

Critical adapters should have contract tests.

For example, every `IProductRepository` implementation should satisfy the same repository behavior contract.

Conceptually:

```text
ProductRepositoryContractTests
        │
        ├── Mongo implementation
        └── future implementation
```

This helps ensure infrastructure replacement does not alter application semantics.

---

# 43. Adapter Resilience

Resilience belongs at outbound integration boundaries.

Examples:

```text
HTTP Adapter
AI Provider Adapter
Freight Provider Adapter
ERP Adapter
```

Depending on semantics, adapters may apply:

```text
Timeout
Retry
Circuit Breaker
Rate Limiting
Fallback
```

Retries must respect idempotency.

---

# 44. Adapter Observability

Outbound adapters should emit observability information such as:

```text
latency
success/failure
dependency name
operation name
retry count
provider name
request correlation
```

Sensitive payloads must not be logged.

---

# 45. AI Observability

AI adapters require additional metadata.

Possible telemetry:

```text
Provider
Model
Operation
Latency
Input token count
Output token count
Estimated cost
Fallback used
Cache hit
Success / Failure
```

Prompt content must follow privacy and security rules.

---

# 46. Adapter Configuration

Adapter configuration uses strongly typed Options.

Examples:

```text
MongoOptions
KafkaOptions
RedisOptions
ElasticOptions
AzureOpenAIOptions
GoogleVertexAIOptions
FreightProviderOptions
```

Configuration classes belong to Infrastructure.

---

# 47. Secrets

Adapters may require secrets.

Examples:

```text
API keys
database credentials
cloud credentials
certificates
```

Secrets must be resolved through secure configuration mechanisms.

Production may use:

```text
Azure Key Vault
Managed Identity
Workload Identity
Kubernetes Secrets
```

Secrets must never appear in Domain models.

---

# 48. Multi-Provider Capability

Where there is a meaningful business or operational reason, a port may support multiple adapters simultaneously.

Example:

```text
IFreightProvider
     │
     ├── CarrierA
     ├── CarrierB
     └── MarketplaceCarrier
```

Application orchestration may select one or aggregate multiple results.

The selection policy must not be hardcoded into vendor-specific adapters.

---

# 49. Adapter Selection Policies

If provider selection is business-relevant, the policy belongs inside Domain/Application.

If selection is purely technical, it belongs in Infrastructure or configuration.

Example business selection:

```text
Use fulfillment provider based on Seller and region.
```

Example technical selection:

```text
Use Google AI when Azure AI is unavailable.
```

These are different concerns and must not be mixed.

---

# 50. Integration Anti-Corruption Example

Conceptual flow:

```text
External Marketplace

externalProductId
merchantCode
salePrice
distributionNode

          │
          ▼

Marketplace Adapter

          │
          ▼

Canonical Input

ProductReference
SellerReference
Money
FulfillmentNodeReference

          │
          ▼

Yunu.Commerce
```

External terminology stops at the adapter boundary.

---

# 51. API Anti-Corruption

Even public Yunu.Commerce APIs should not expose Domain Entities directly.

API models act as adapters between:

```text
External Consumer Contract
          │
          ▼
Application Contract
          │
          ▼
Domain
```

This prevents API evolution from forcing Domain changes.

---

# 52. Event Anti-Corruption

Integration events from external platforms must be translated before internal processing.

Example:

```text
External Event
     │
     ▼
External Kafka Adapter
     │
     ▼
Canonical Integration Message
     │
     ▼
Application Command
```

Do not process foreign event schemas directly inside Domain code.

---

# 53. Database Anti-Corruption

Legacy database schemas are external models too.

Example:

```text
LEGACY_PRD_001
CD_SKU
VL_PRC
IND_ATIVO
```

should be translated through infrastructure mappings into canonical concepts.

Legacy schemas must not dictate Domain naming.

---

# 54. No Infrastructure Service Locator

Adapters must be injected explicitly.

Do not hide infrastructure behind static global access such as:

```text
Infrastructure.Current.Mongo
Infrastructure.Current.Redis
```

Use explicit ports and Dependency Injection.

---

# 55. No Static Vendor SDK Access

Avoid vendor SDK usage from static helper classes called from Application.

Forbidden conceptual example:

```text
AzureOpenAIHelper.Generate(...)
```

called directly by Application.

Preferred:

```text
IGenerativeAIProvider
```

injected into the Application use case.

---

# 56. Adapter Failure Translation

Technical failures must be translated appropriately.

Example:

```text
MongoConnectionException
       │
       ▼
Infrastructure error handling
       │
       ▼
Application-level failure
```

Provider-specific exceptions must not escape uncontrolled into Domain logic.

---

# 57. Idempotent Inbound Adapters

Inbound messaging adapters must assume duplicate delivery where applicable.

Example:

```text
Kafka Event
   │
   ▼
Inbox / Idempotency Check
   │
   ▼
Application Command
```

Duplicate delivery must not corrupt business state.

---

# 58. Inbound Adapter Authentication

Authentication belongs to inbound adapters.

Examples:

```text
JWT validation
API key validation
mTLS
OAuth
```

The Domain should operate on trusted identity/context abstractions rather than transport credentials.

---

# 59. Authorization Boundary

Authorization may span Application and Domain depending on semantics.

Transport authorization:

```text
Can this token call this endpoint?
```

belongs to API/Application security.

Business authorization:

```text
Can this Seller modify this Offer?
```

may belong to Application or Domain rules.

Do not confuse authentication infrastructure with business permission logic.

---

# 60. Hexagonal Architecture and Bounded Contexts

Each Bounded Context has its own hexagon.

Conceptually:

```text
┌────────────────────┐
│      Catalog       │
│ Domain/Application │
└─────────┬──────────┘
          │
       Adapters
```

and independently:

```text
┌────────────────────┐
│      Pricing       │
│ Domain/Application │
└─────────┬──────────┘
          │
       Adapters
```

There is not one giant global hexagon containing every business capability.

---

# 61. Hexagonal Architecture and Clean Architecture

Clean Architecture defines dependency direction.

Hexagonal Architecture defines system boundaries and interaction points.

They complement each other.

Conceptually:

```text
Clean Architecture
    defines inward dependencies

Hexagonal Architecture
    defines Ports & Adapters
```

Yunu.Commerce applies both simultaneously.

---

# 62. Hexagonal Architecture and EDA

Kafka is an adapter.

Events themselves are contracts.

Example:

```text
Application
     │
     ▼
IIntegrationEventPublisher
     │
     ▼
Kafka Adapter
     │
     ▼
Kafka
```

The core knows that an event must be published.

It does not know how Kafka works.

---

# 63. Hexagonal Architecture and AI

AI providers are adapters.

This is strategically important because AI technology evolves quickly.

Conceptually:

```text
Commerce AI Use Case
        │
        ▼
AI Port
        │
        ├──── Azure
        ├──── Google
        └──── Future Provider
```

Provider change must remain an infrastructure decision whenever possible.

---

# 64. Hexagonal Architecture and Search

Elasticsearch is an adapter.

Search Application defines what search capabilities are required.

Infrastructure decides how Elasticsearch executes them.

This allows future migration without changing higher-level business semantics.

---

# 65. Hexagonal Architecture and Persistence

Database technology is never the architecture.

Example:

```text
Catalog Domain
     │
     ▼
Repository Port
     │
     ▼
Mongo Adapter
```

MongoDB may be replaced.

Catalog remains Catalog.

---

# 66. Port Naming

Ports should use business or capability language.

Good:

```text
IProductRepository
IProductSearch
IGenerativeAIProvider
IFreightProvider
IObjectStorage
```

Avoid technology-oriented port names:

```text
IMongoService
IRedisService
IKafkaService
IAzureAIService
```

Technology belongs in adapter names.

---

# 67. Adapter Naming

Adapters should clearly reveal their implementation technology or external system.

Examples:

```text
MongoProductRepository
RedisCacheProvider
KafkaIntegrationEventPublisher
ElasticsearchProductSearch
AzureOpenAIGenerativeAIProvider
GoogleVertexAIGenerativeAIProvider
SapCatalogAdapter
```

This makes boundaries visible in the codebase.

---

# 68. Port Location Rule

A port belongs in the innermost layer that needs the capability.

Examples:

Repository needed to reconstitute Aggregate:

```text
Domain
```

AI provider needed by Application use case:

```text
Application
```

Search indexer needed by Search Application:

```text
Application
```

Do not centralize every port in `SharedKernel`.

---

# 69. Adapter Location Rule

Adapters belong to Infrastructure or Host boundaries.

Examples:

```text
Catalog.Infrastructure.Persistence.Mongo
AI.Infrastructure.Providers.AzureOpenAI
Search.Infrastructure.Elasticsearch
Integrations.Infrastructure.Sap
```

Folder hierarchy should communicate architecture.

---

# 70. Architecture Tests

Architecture tests should enforce Hexagonal rules where practical.

Examples:

```text
Application must not reference MongoDB.Driver

Application must not reference StackExchange.Redis

Application must not reference Elasticsearch SDK

AI.Application must not reference Azure AI SDK

AI.Application must not reference Google Cloud AI SDK

Domain must not reference infrastructure adapters
```

Violations should fail CI.

---

# 71. Initial Port Candidates

The initial architecture skeleton may define only high-confidence abstractions.

Potential candidates include:

```text
IClock
IIntegrationEventPublisher
```

Other ports should be introduced with actual use cases.

Do not create every future interface during scaffolding.

For example:

```text
IProductRepository
IGenerativeAIProvider
IProductIndexer
```

should be introduced when their respective vertical slices are implemented.

Avoid speculative architecture.

---

# 72. Initial Adapter Candidates

During the initial solution skeleton, Infrastructure projects may exist without full adapter implementations.

The first meaningful adapters are expected during the Catalog vertical slice.

Likely initial implementation sequence:

```text
MongoProductRepository
       ↓
Kafka Event Infrastructure
       ↓
Azure or Google AI Provider
       ↓
Elasticsearch Product Indexer
```

Adapters must be added incrementally.

---

# 73. Adapter Completion Criteria

An adapter is not complete merely because it compiles.

It should include, when relevant:

```text
Configuration
Dependency Injection
Validation
Resilience
Observability
Cancellation support
Error translation
Integration tests
Security handling
```

---

# 74. Core Architectural Rule

The core Hexagonal Architecture rule of Yunu.Commerce is:

> The core never asks which technology is being used.

It asks only for capabilities.

Examples:

```text
Persist this Product.

Publish this Event.

Search these Products.

Generate this AI enrichment.

Calculate this Freight quote.
```

Adapters decide how those capabilities are fulfilled.

---

# 75. Final Principle

External technologies are replaceable edges.

The Yunu.Commerce business model is the stable center.

The architecture must continuously protect that distinction.
