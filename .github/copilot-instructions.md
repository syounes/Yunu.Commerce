# Yunu.Commerce - GitHub Copilot Instructions

## 1. Project Identity

Project name:

`Yunu.Commerce`

Yunu.Commerce is a modular, enterprise-grade commerce platform designed for product catalog, SKUs, sellers, offers, pricing, availability, freight, search and Generative AI capabilities.

The platform must be designed as a reusable commerce product and MUST NOT contain business rules tied to a specific retailer, marketplace, ERP, cloud provider or customer.

External systems must integrate with Yunu.Commerce through adapters and anti-corruption layers.

---

# 2. Copilot Role

GitHub Copilot acts as an implementation assistant.

Copilot MUST NOT independently redesign the system architecture.

Architectural decisions are defined by the project documentation and must be followed.

Before generating or modifying code, Copilot must:

1. Identify the bounded context affected.
2. Identify the architectural layer affected.
3. Verify allowed dependencies.
4. Verify whether an existing abstraction, contract or building block already exists.
5. Preserve domain boundaries.
6. Avoid introducing unnecessary dependencies.
7. Avoid duplicating existing functionality.
8. Prefer simple and explicit implementations.
9. Follow existing naming and folder conventions.
10. Preserve backwards compatibility unless explicitly instructed otherwise.

If a requested implementation conflicts with these architectural rules, Copilot should point out the conflict before implementing it.

---

# 3. Architecture

The architecture combines:

- Domain-Driven Design (DDD)
- Clean Architecture
- Hexagonal Architecture
- Event-Driven Architecture (EDA)
- CQRS
- Ports and Adapters
- Anti-Corruption Layer
- Polyglot Persistence
- Distributed Systems principles

The architecture must prioritize:

- clear domain boundaries
- low coupling
- high cohesion
- testability
- observability
- resilience
- scalability
- maintainability
- replaceable infrastructure
- cloud portability

---

# 4. Dependency Rule

Dependencies always point inward.

The conceptual dependency direction is:

Infrastructure
→ Application
→ Domain

Hosts and APIs compose the application.

The Domain layer MUST NOT depend on:

- ASP.NET Core
- Entity Framework Core
- MongoDB.Driver
- Kafka libraries
- Redis libraries
- Elasticsearch clients
- Azure SDKs
- Google Cloud SDKs
- OpenAI SDKs
- HTTP clients
- filesystem implementations
- external APIs
- logging frameworks
- cloud infrastructure

The Domain must remain pure C# whenever possible.

---

# 5. Domain Layer

The Domain layer contains business concepts and business behavior.

Allowed concepts include:

- Aggregates
- Aggregate Roots
- Entities
- Value Objects
- Domain Services
- Domain Events
- Specifications
- Domain Exceptions
- Business Rules
- Repository contracts when required by the domain
- Domain policies

The Domain layer MUST NOT contain infrastructure implementations.

Domain entities must contain behavior.

Avoid anemic domain models where entities are only property containers.

Bad example:

```csharp
product.Price = newPrice;
```

Prefer domain behavior:

```csharp
product.ChangePrice(newPrice);
```

when price belongs to that aggregate and business rules must be enforced.

Business invariants must be protected inside the domain.

---

# 6. Value Objects

Use Value Objects when a concept has meaning beyond its primitive representation.

Examples:

- ProductId
- SkuId
- SellerId
- OfferId
- BranchId
- Money
- Currency
- Region
- PostalCode
- PaymentMethod
- Installment
- Percentage
- DateRange

Avoid primitive obsession.

Prefer:

```csharp
Money price
```

instead of:

```csharp
decimal price
string currency
```

when the business concept requires validation or behavior.

---

# 7. Application Layer

The Application layer orchestrates use cases.

It may contain:

- Commands
- Queries
- Command Handlers
- Query Handlers
- DTOs
- Application Services
- Validators
- Mappers
- Ports
- Application interfaces
- Transaction abstractions
- Authorization policies
- Pipeline behaviors

The Application layer MUST NOT contain core business rules that belong to the Domain.

Application coordinates.

Domain decides.

Example:

Application:

```text
Receive command
Load aggregate
Call domain behavior
Persist aggregate
Publish integration event
```

Domain:

```text
Validate invariant
Execute state transition
Generate domain event
```

---

# 8. CQRS

Commands change state.

Queries read state.

Examples of commands:

- CreateProductCommand
- UpdateProductCommand
- CreateSkuCommand
- ActivateSkuCommand
- CreateOfferCommand
- ChangePriceCommand
- ChangeRegionalPriceCommand
- UpdateAvailabilityCommand
- UpdateBranchAvailabilityCommand

Examples of queries:

- GetProductQuery
- GetSkuQuery
- SearchProductsQuery
- GetOfferQuery
- GetPriceQuery
- GetAvailabilityQuery
- GetFreightQuoteQuery

Do not create artificial CQRS complexity for trivial internal operations.

CQRS is used at application boundaries and where separation improves the architecture.

---

# 9. Hexagonal Architecture

External technology must be represented behind ports and adapters.

Examples of ports:

```csharp
IProductRepository
ISkuRepository
IOfferRepository
IPriceRepository
IAvailabilityRepository

IEventPublisher
ICacheProvider
ISearchIndexer

IGenerativeAIProvider
IEmbeddingProvider

IFreightProvider
IExternalCatalogProvider
```

Examples of adapters:

```text
MongoProductRepository
SqlPriceRepository
RedisCacheProvider
KafkaEventPublisher
ElasticSearchIndexer

AzureOpenAIProvider
GoogleVertexAIProvider

SapCatalogAdapter
VtexCatalogAdapter
MarketplaceCatalogAdapter
```

Business layers must depend on abstractions, never directly on infrastructure implementations.

---

# 10. Anti-Corruption Layer

External data models MUST NOT leak into the Yunu.Commerce Domain.

Example external model:

```text
SAP
MATNR
WERKS
VKORG
```

These values must be translated by an adapter into the canonical Yunu.Commerce model.

Example:

```text
SAP Model
   ↓
SapProductAdapter
   ↓
Yunu Canonical Product
```

The same rule applies to:

- ERP
- OMS
- WMS
- marketplaces
- payment systems
- freight providers
- customer APIs
- external databases

---

# 11. Canonical Commerce Model

Yunu.Commerce owns its canonical business language.

Core concepts include:

## Catalog

- Product
- SKU
- Category
- Brand
- Attribute
- Specification
- Media
- Catalog

## Seller

- Seller
- Merchant
- First Party (1P)
- Third Party (3P)

## Offers

- Offer
- Seller Offer
- Offer Status
- Commercial Conditions

## Pricing

- National Price
- Regional Price
- Promotional Price
- Payment Price
- PIX Price
- Boleto Price
- Credit Card Price
- Installments

## Availability

- National Availability
- Regional Availability
- Branch Availability
- Stock Position

## Fulfillment

- Branch
- Store
- Warehouse
- Distribution Center
- Fulfillment Node

## Freight

- Freight Quote
- Freight Option
- Delivery Method
- Carrier
- Service Level
- Delivery Promise

---

# 12. Bounded Contexts

The initial bounded contexts are:

```text
Catalog
Sellers
Offers
Pricing
Availability
Fulfillment
Freight
Search
AI
Integrations
```

Bounded contexts must not directly access each other's databases.

Communication between contexts must happen through:

- application APIs
- contracts
- integration events
- asynchronous messaging

Direct cross-context database queries are prohibited.

---

# 13. Event-Driven Architecture

Kafka is the initial event streaming platform.

Business operations may generate Domain Events.

Domain Events are internal to a bounded context.

Integration Events communicate changes between bounded contexts.

Examples:

```text
ProductCreated
ProductUpdated

SkuCreated
SkuActivated

OfferCreated
OfferChanged

PriceChanged
RegionalPriceChanged

AvailabilityChanged
BranchAvailabilityChanged

FreightUpdated

ProductIndexed
CatalogPublished
```

Events must represent facts that already happened.

Prefer:

`ProductCreated`

instead of:

`CreateProduct`

for events.

---

# 14. Event Envelope

Integration events should follow a common envelope.

Minimum metadata:

```text
EventId
EventType
AggregateId
AggregateType
CorrelationId
CausationId
OccurredAtUtc
SchemaVersion
Source
Data
```

All event timestamps must use UTC.

Events must be versionable.

Never silently break an existing event contract.

---

# 15. Transactional Outbox

Database changes and integration events must not rely on unsafe dual writes.

When appropriate use the Transactional Outbox Pattern.

Conceptual flow:

```text
Application Transaction
    ↓
Aggregate changes
+
OutboxMessage
    ↓
Commit
    ↓
Outbox Processor
    ↓
Kafka
```

---

# 16. Consumer Idempotency

Kafka consumers must be designed for at-least-once delivery.

Consumers must tolerate duplicate messages.

Use techniques such as:

- Inbox Pattern
- processed event identifiers
- idempotent operations
- database constraints
- deterministic state updates

Never assume an integration event will arrive exactly once.

---

# 17. Data Architecture

Yunu.Commerce uses polyglot persistence.

A database technology is selected according to the problem being solved.

Initial intended technologies:

## Relational

SQL Server, PostgreSQL or Azure SQL where relational consistency is appropriate.

Possible use cases:

- sellers
- commercial relationships
- pricing rules
- structured commercial configuration

## Document

MongoDB for flexible document structures.

Possible use cases:

- products
- SKUs
- catalog structures
- product attributes
- projections

## Cache

Redis.

Possible use cases:

- availability cache
- pricing cache
- freight cache
- search cache
- distributed locks
- rate limiting
- semantic AI cache

## Search

Elasticsearch.

Used as search and read-optimized infrastructure.

Elasticsearch MUST NOT automatically become the system of record.

## Event Streaming

Kafka.

## Object Storage

Used for:

- product assets
- imports
- exports
- documents
- AI source documents

---

# 18. Search Architecture

Search is a derived read model.

Typical flow:

```text
Catalog
   ↓
ProductUpdated
   ↓
Kafka
   ↓
Search Indexer
   ↓
Elasticsearch
```

Search models may combine information coming from multiple bounded contexts.

Search documents should be optimized for querying, not domain modeling.

---

# 19. Generative AI Architecture

Generative AI must be provider-independent.

Business code MUST NOT directly depend on Azure OpenAI, Google Vertex AI or another LLM provider.

Define abstractions such as:

```csharp
IGenerativeAIProvider
IEmbeddingProvider
IAIModelRouter
IPromptTemplateProvider
```

Infrastructure may implement:

```text
AzureOpenAIProvider
GoogleVertexAIProvider
```

The provider must be replaceable through configuration and dependency injection.

---

# 20. AI Gateway

LLM access should eventually pass through a centralized AI Gateway / AI orchestration layer.

Responsibilities may include:

- provider selection
- model selection
- prompt templates
- tool execution
- token control
- cost tracking
- rate limiting
- retries
- fallback
- guardrails
- tracing
- semantic cache

Domain services must never call an LLM directly.

---

# 21. AI Catalog Enrichment

Generative AI may enrich product catalog information.

Possible enrichment capabilities:

- title normalization
- description generation
- technical summary
- category classification
- attribute extraction
- tags
- SEO metadata
- keywords
- FAQ generation
- duplicate detection
- data quality analysis
- inconsistency detection

AI-generated information must not silently overwrite source-of-truth information.

Prefer a workflow such as:

```text
Original Product
      ↓
AI Enrichment
      ↓
Proposed Enrichment
      ↓
Validation / Approval
      ↓
Catalog Update
```

---

# 22. AI Tools

AI agents must interact with commerce through explicit application tools.

Potential tools:

```text
search_products
get_product
get_sku
get_offer
get_price
get_availability
get_freight
compare_products
find_similar_products
```

The LLM must not query commerce databases directly.

AI → Application Tool → Application Layer → Domain/Query Infrastructure.

---

# 23. RAG

Retrieval-Augmented Generation may use:

- catalog information
- product descriptions
- specifications
- offers
- pricing
- availability
- freight
- manuals
- policies
- FAQ
- seller information

Answers involving current commerce information must rely on retrieved information or tool execution instead of LLM memory.

---

# 24. Embeddings

Embeddings may be generated for:

- product descriptions
- technical specifications
- categories
- attributes
- manuals
- FAQs

Vector infrastructure must remain replaceable behind abstractions.

Hybrid search may combine:

```text
Lexical Search
+
Vector Search
+
Business Ranking
```

---

# 25. Resilience

External communication must consider resilience.

Depending on the operation use:

- Timeout
- Retry
- Circuit Breaker
- Rate Limiting
- Bulkhead
- Fallback

Retries must not be blindly applied to non-idempotent operations.

---

# 26. Observability

Observability is mandatory from the beginning.

Use OpenTelemetry concepts for:

- Logs
- Metrics
- Distributed Traces

Preserve:

```text
TraceId
CorrelationId
CausationId
```

across distributed operations whenever applicable.

Do not log:

- passwords
- secrets
- access tokens
- private keys
- sensitive customer information

---

# 27. Security

Secrets must never be committed to source control.

Cloud production environments should use mechanisms such as:

- Managed Identity
- Key Vault
- workload identity
- RBAC

Authentication architecture may use:

- OAuth 2.0
- OpenID Connect
- JWT
- Microsoft Entra ID

Infrastructure concerns must remain outside the Domain.

---

# 28. Date and Time Rules

All system timestamps must be stored and transported in UTC unless there is a documented business reason otherwise.

Prefer:

```csharp
DateTimeOffset
```

for timestamps crossing system boundaries.

Avoid ambiguous local `DateTime` usage.

Business timezone conversions must occur explicitly at the appropriate boundary.

---

# 29. Money Rules

Never use floating point types for monetary values.

Use decimal-backed monetary Value Objects.

Money must always include its currency when currency matters.

Prices should never be represented as raw primitive values throughout the Domain.

---

# 30. Nullable Reference Types

Nullable reference types must be enabled.

Nullability warnings must be treated seriously.

Do not silence nullable warnings with `!` unless the invariant is genuinely guaranteed and documented.

Prefer correct modeling instead of warning suppression.

---

# 31. Async Rules

All I/O operations must use async APIs.

Async methods must normally accept:

```csharp
CancellationToken cancellationToken
```

Do not use:

```csharp
.Result
.Wait()
.GetAwaiter().GetResult()
```

inside asynchronous application code.

---

# 32. Dependency Injection

Use constructor injection.

Avoid service locator patterns.

Avoid resolving dependencies manually from `IServiceProvider` inside business logic.

Infrastructure registrations should be organized by module.

---

# 33. Configuration

Configuration must use strongly typed options where appropriate.

Example:

```csharp
services.Configure<KafkaOptions>(...);
```

Avoid scattering configuration keys throughout application code.

Secrets must not be stored in regular application configuration committed to Git.

---

# 34. Error Handling

Use domain-specific exceptions only when exceptions are appropriate.

Expected validation failures should preferably be represented explicitly.

HTTP-specific concepts must not leak into Domain or Application logic.

Do not throw:

```csharp
BadRequestException
NotFoundHttpException
```

from Domain entities.

Translate application/domain outcomes into HTTP responses at the API boundary.

---

# 35. Logging

Use structured logging.

Prefer:

```csharp
logger.LogInformation(
    "Product {ProductId} created for seller {SellerId}",
    productId,
    sellerId);
```

instead of string interpolation.

Logs must carry relevant correlation information.

---

# 36. Testing Strategy

All important domain rules require unit tests.

Testing layers include:

```text
Domain Unit Tests
Application Unit Tests
Architecture Tests
Integration Tests
Contract Tests
Infrastructure Integration Tests
API Tests
```

Domain tests should not require:

- MongoDB
- SQL Server
- Kafka
- Redis
- Elasticsearch
- Azure
- Google Cloud

Infrastructure integration tests may use Testcontainers where appropriate.

---

# 37. Architecture Tests

Add architecture tests that automatically enforce dependency rules.

Examples:

- Domain cannot reference Infrastructure.
- Domain cannot reference ASP.NET Core.
- Application cannot depend on Infrastructure implementations.
- Bounded Context A cannot directly reference Infrastructure of Bounded Context B.
- Domain must not reference MongoDB.Driver.
- Domain must not reference EF Core.

Architecture rules should be executable whenever practical.

---

# 38. Code Quality

Generated code must prioritize readability.

Avoid:

- unnecessary abstractions
- unnecessary inheritance
- giant classes
- giant interfaces
- static service classes
- magic strings
- magic numbers
- duplicated logic
- hidden side effects
- speculative abstractions

Prefer composition over inheritance.

Follow SOLID principles pragmatically.

Design Patterns must solve real problems.

Never add a Design Pattern only to demonstrate that the pattern exists.

---

# 39. Naming

Use English for:

- source code
- classes
- methods
- properties
- namespaces
- projects
- APIs
- event names
- tests
- technical documentation identifiers

Examples:

```text
Product
CreateProductCommand
ProductCreatedIntegrationEvent
IProductRepository
MongoProductRepository
```

Avoid Portuguese identifiers in code.

Architecture discussions and explanatory documentation may use Portuguese when appropriate.

---

# 40. Solution Naming

Root solution:

```text
Yunu.Commerce.sln
```

Root namespace:

```text
Yunu.Commerce
```

Project naming convention:

```text
Yunu.Commerce.{BoundedContext}.{Layer}
```

Example:

```text
Yunu.Commerce.Catalog.Domain
Yunu.Commerce.Catalog.Application
Yunu.Commerce.Catalog.Infrastructure
Yunu.Commerce.Catalog.Api
Yunu.Commerce.Catalog.Contracts
Yunu.Commerce.Catalog.Worker
```

---

# 41. Initial Solution Structure

Expected high-level structure:

```text
Yunu.Commerce
│
├── .github
│   ├── copilot-instructions.md
│   └── instructions
│
├── docs
│   ├── architecture
│   ├── adr
│   ├── domains
│   ├── integration
│   ├── ai
│   ├── data
│   ├── security
│   └── observability
│
├── src
│
│   ├── BuildingBlocks
│   │
│   ├── Catalog
│   ├── Sellers
│   ├── Offers
│   ├── Pricing
│   ├── Availability
│   ├── Fulfillment
│   ├── Freight
│   ├── Search
│   ├── AI
│   └── Integrations
│
├── tests
│
├── deploy
│   ├── docker
│   ├── kubernetes
│   └── helm
│
└── Yunu.Commerce.sln
```

---

# 42. Building Blocks

Shared building blocks must remain small.

Potential building blocks:

```text
Yunu.Commerce.SharedKernel
Yunu.Commerce.Contracts
Yunu.Commerce.EventBus
Yunu.Commerce.Observability
Yunu.Commerce.Security
```

Do NOT create a giant shared project containing unrelated utilities.

Shared Kernel must only contain concepts that truly have the same meaning across bounded contexts.

Prefer duplication over incorrect domain coupling.

---

# 43. Modular First

Do not automatically create a large number of independently deployable microservices.

The initial architecture should be modular and distribution-ready.

Boundaries must be strong enough that modules can later become independent services without redesigning the Domain.

Distribution is an operational decision.

Domain boundaries are business decisions.

Do not confuse the two.

---

# 44. No Cross-Database Coupling

A module must never directly read another bounded context's private database.

Forbidden example:

```text
Pricing
   ↓
SELECT *
FROM Catalog.Products
```

Instead use:

```text
Catalog
   ↓
Integration Event
   ↓
Pricing Projection
```

or an explicit API/query contract when synchronous consistency is necessary.

---

# 45. Infrastructure Replaceability

Infrastructure implementations must be replaceable.

Examples:

```text
MongoDB ↔ another document database

SQL Server ↔ PostgreSQL

Redis ↔ another distributed cache

Elasticsearch ↔ another search provider

Kafka ↔ another event broker

Azure OpenAI ↔ Google Vertex AI
```

Do not expose provider-specific types through Domain or Application interfaces.

---

# 46. API Versioning

Public APIs must be designed for versioning.

Example:

```text
/api/v1/products
/api/v1/skus
/api/v1/offers
/api/v1/prices
/api/v1/availability
/api/v1/freight
```

Breaking API changes require explicit versioning decisions.

---

# 47. Backward Compatibility

Before changing:

- public API contracts
- integration events
- persistence schemas
- shared contracts

Copilot must evaluate compatibility impact.

Do not silently rename or remove existing fields in published contracts.

---

# 48. Database Migrations

Relational database schema changes must use controlled migrations.

Production schema changes must not depend on automatic destructive database initialization.

Document database schema evolution must be version-aware where required.

---

# 49. Generated Code Policy

Before generating substantial code, Copilot should determine:

```text
WHAT bounded context?
WHAT use case?
WHAT layer?
WHAT existing abstraction?
WHAT dependency direction?
WHAT tests are required?
```

Copilot must avoid generating hundreds of speculative files.

Implement incrementally.

Prefer compiling architecture over theoretical architecture.

---

# 50. Build Integrity

After meaningful code changes Copilot should, when tools are available:

1. restore dependencies
2. build the solution
3. run relevant tests
4. report compilation failures
5. fix failures caused by the change

Generated code is not considered complete when the solution does not compile.

---

# 51. Warning Policy

Do not ignore compiler warnings introduced by new code.

Do not disable analyzers merely to make the build green.

Fix the underlying issue unless a suppression has a documented architectural reason.

---

# 52. Documentation

Important architectural decisions must be documented.

Use Architecture Decision Records under:

```text
/docs/adr
```

Examples:

```text
ADR-001-use-ddd-clean-hexagonal.md
ADR-002-use-kafka-for-integration-events.md
ADR-003-use-transactional-outbox.md
ADR-004-use-mongodb-for-catalog.md
ADR-005-use-elasticsearch-for-product-search.md
ADR-006-ai-provider-abstraction.md
```

Do not change an accepted architectural decision silently.

Create or update an ADR.

---

# 53. Comments

Code should normally explain itself through good naming and structure.

Comments should explain:

- architectural intent
- non-obvious business rules
- unusual technical constraints
- important tradeoffs

Do not add comments that simply repeat the code.

---

# 54. Pull Request Mental Checklist

Before considering an implementation complete, verify:

- Does the code belong to this bounded context?
- Is the layer correct?
- Are dependency rules respected?
- Are domain rules inside the Domain?
- Did provider-specific code leak inward?
- Is asynchronous I/O cancellable?
- Is money modeled safely?
- Are dates UTC-safe?
- Are events idempotent?
- Is observability present?
- Are tests included?
- Does the solution compile?
- Did we introduce unnecessary complexity?
- Did we unintentionally break a contract?

---

# 55. Primary Architectural Principle

The most important rule of the Yunu.Commerce codebase is:

> The business model owns the architecture. Infrastructure serves the business model.

Azure, Google, MongoDB, SQL Server, Kafka, Redis, Elasticsearch and LLM providers are implementation details.

They may change.

The Yunu.Commerce Domain must survive those changes.

---

# 56. Copilot Behavior When Unsure

If architectural information is missing:

1. Search the repository documentation.
2. Search existing code and contracts.
3. Follow established patterns.
4. Do not invent a new architectural convention without justification.
5. State assumptions when required.
6. Prefer the smallest implementation compatible with the architecture.

Never create a new cross-cutting framework because one use case required three lines of duplicated code.

---

# 57. Current Project Phase

The current project phase is:

`Architecture and Solution Skeleton`

During this phase:

DO create:

- solution
- project structure
- project references
- baseline packages
- dependency injection composition
- architecture tests
- common configuration
- basic observability plumbing
- local infrastructure definitions
- documentation structure

DO NOT yet invent complete business implementations.

Domain Aggregates, Domain Services, Entities, detailed business rules and application use cases will be introduced incrementally after the architecture skeleton is validated.

The goal of the current phase is to create a clean, compiling architectural shell that can safely receive domain implementations later.

---

# 58. Final Instruction

When instructed to create the initial Yunu.Commerce solution skeleton:

1. Follow this document.
2. Create only justified projects.
3. Configure project references according to Clean Architecture.
4. Add required baseline dependencies.
5. Keep Domain projects infrastructure-free.
6. Add architecture tests.
7. Ensure the entire solution builds.
8. Do not fabricate business rules.
9. Do not implement speculative domain models.
10. Report what was created and any architectural assumptions.

Architecture first.

Implementation second.

Infrastructure third.

Optimization only after evidence.

- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.
