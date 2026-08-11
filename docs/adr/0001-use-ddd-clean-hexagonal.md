# ADR-0001: Use DDD, Clean Architecture and Hexagonal Architecture

- **Status:** Accepted
- **Date:** 2026-08-11
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Entire Yunu.Commerce solution

## 1. Context

Yunu.Commerce is a commerce platform designed to support multiple business capabilities, including:

- Catalog and SKU management
- Sellers and 1P / 3P commerce
- Offers
- National and regional pricing
- Payment-specific prices such as PIX and Boleto
- National and regional availability
- Fulfillment nodes and branches
- Regional freight
- Search projections
- Event-driven integration
- Generative AI assisted catalog onboarding and enrichment

The solution must remain maintainable as business complexity, integrations, infrastructure technologies and AI providers evolve.

The architecture must prevent business rules from becoming coupled to frameworks, databases, messaging technologies, cloud providers or Generative AI SDKs.

## 2. Decision

Yunu.Commerce will combine:

```text
Domain-Driven Design
        +
Clean Architecture
        +
Hexagonal Architecture
        +
Event-Driven Architecture
```

These approaches have different responsibilities and are complementary.

### Domain-Driven Design

DDD defines how the business is modeled.

It provides concepts such as:

```text
Bounded Context
Aggregate
Aggregate Root
Entity
Value Object
Domain Service
Domain Event
Repository abstraction
Ubiquitous Language
```

### Clean Architecture

Clean Architecture defines dependency direction.

The fundamental dependency rule is:

```text
Infrastructure
     │
     ▼
Application
     │
     ▼
Domain
```

Dependencies point toward the business core.

The Domain must not depend on Infrastructure.

### Hexagonal Architecture

Hexagonal Architecture defines how the application communicates with external technologies through Ports and Adapters.

Conceptually:

```text
             REST API
                │
                ▼
        ┌───────────────┐
        │  Application  │
        └───────┬───────┘
                │
                ▼
             Domain
                │
        ┌───────┴────────┐
        ▼                ▼
 Repository Port     AI Provider Port
        │                │
        ▼                ▼
     MongoDB        Azure / Google
```

### Event-Driven Architecture

EDA provides asynchronous integration between Bounded Contexts and external consumers.

Conceptually:

```text
Domain Change
     │
     ▼
Domain Event
     │
     ▼
Application
     │
     ▼
Integration Event
     │
     ▼
Outbox
     │
     ▼
Kafka
```

## 3. Why These Architectures Are Combined

The approaches solve different architectural problems.

```text
DDD
→ How do we model the business?

Clean Architecture
→ Which direction may dependencies flow?

Hexagonal Architecture
→ How do we isolate external technologies?

EDA
→ How do independently owned capabilities communicate asynchronously?
```

Using them together creates a coherent architecture rather than four competing architectures.

## 4. Dependency Rule

The following dependency direction is mandatory:

```text
Domain
  ▲
  │
Application
  ▲
  │
Infrastructure
  ▲
  │
Host / API / Worker
```

Inner layers must never depend on outer layers.

## 5. Domain Layer

The Domain layer contains business concepts and rules.

It may contain:

```text
Aggregates
Entities
Value Objects
Domain Services
Domain Events
Domain Exceptions
Domain Policies
Repository contracts where appropriate
Specifications where appropriate
```

The Domain must remain independent from implementation technologies.

## 6. Domain Layer Forbidden Dependencies

Domain projects must not reference:

```text
ASP.NET Core
Entity Framework Core
MongoDB.Driver
StackExchange.Redis
Elasticsearch clients
Kafka clients
Azure SDKs
Google AI SDKs
HTTP clients
Controllers
DbContext
Infrastructure projects
```

If replacing MongoDB with another database requires changing an Aggregate, the boundary has been violated.

If replacing Azure AI with Google AI requires changing Catalog Domain, the boundary has been violated.

## 7. Application Layer

The Application layer orchestrates use cases.

It may contain:

```text
Commands
Queries
Handlers
Use Cases
Application Services
Validators
DTOs
Ports
Repository usage
Transaction abstractions
Authorization orchestration
Integration event mapping
```

Application coordinates Domain behavior but does not own core business invariants.

## 8. Application Rule

Application code should express intent.

Example:

```text
CreateProductCommand
        │
        ▼
CreateProductHandler
        │
        ▼
Product Aggregate
        │
        ▼
IProductRepository
```

The handler coordinates the operation.

The Product Aggregate protects Product invariants.

## 9. Infrastructure Layer

Infrastructure implements technical adapters.

Examples:

```text
MongoDB repositories
SQL repositories
EF Core DbContexts
Dapper queries
Redis cache adapters
Elasticsearch adapters
Kafka producers and consumers
Outbox persistence
Azure AI adapters
Google AI adapters
Object Storage adapters
External REST clients
```

Infrastructure depends on abstractions defined by inner layers.

## 10. Host Layer

Hosts expose executable entry points.

Examples:

```text
ASP.NET Core API
Kafka Worker
Outbox Publisher Worker
Search Projection Worker
AI Processing Worker
Background Service
```

Hosts are composition roots.

They configure dependency injection, middleware, telemetry, configuration and process lifecycle.

## 11. Ports

Ports define what the application needs without specifying the technology.

Examples:

```text
IProductRepository
IProductSearch
ICache
IEventPublisher
IGenerativeAiProvider
IEmbeddingGenerator
IVectorStore
ICarrierQuotationProvider
IObjectStorage
```

Ports must use provider-neutral language.

Bad:

```text
IMongoProductRepository
IRedisAvailabilityCache
IAzureOpenAiService
```

Preferred:

```text
IProductRepository
IAvailabilityCache
IGenerativeAiProvider
```

Concrete technology belongs to adapters.

## 12. Adapters

Adapters implement Ports.

Examples:

```text
MongoProductRepository
RedisAvailabilityCache
ElasticsearchProductSearch
KafkaEventPublisher
AzureGenerativeAiProvider
GoogleGenerativeAiProvider
CarrierXQuotationAdapter
```

Adapters translate between external technology models and Yunu.Commerce contracts.

## 13. Bounded Context Independence

Each Bounded Context owns its business model.

Initial contexts include:

```text
Catalog
Sellers
Offers
Pricing
Availability
Fulfillment
Freight
```

A context must not directly reference another context's Domain model.

Forbidden:

```text
Pricing.Domain
    ↓
Catalog.Domain.Product
```

Instead use:

```text
ProductId
SkuId
OfferId
SellerId
Integration Event
Application Contract
Read Projection
```

## 14. No Shared Database Integration

Bounded Contexts must not integrate by reading or updating each other's tables or collections.

Forbidden:

```text
Pricing
   │
   ▼
SELECT * FROM Catalog.Products
```

Preferred:

```text
Catalog
   │
   ▼
ProductUpdated
   │
   ▼
Kafka
   │
   ▼
Pricing / Search / other consumer
```

Synchronous APIs may be used when immediate information is genuinely required.

## 15. Aggregate Rule

Aggregates define transactional consistency boundaries.

A transaction should normally modify one Aggregate.

Avoid designing giant Aggregates merely to obtain database transactions.

Cross-Aggregate workflows should use:

```text
Application orchestration
Domain Events
Integration Events
Process Managers / Sagas when justified
```

## 16. Repository Rule

Repositories persist Aggregates.

Repositories should not become generic database wrappers.

Avoid:

```text
IGenericRepository<T>
```

when it erases Domain intent.

Prefer meaningful contracts:

```text
IProductRepository
IPriceRepository
IAvailabilityRepository
```

## 17. CQRS

Yunu.Commerce may apply CQRS pragmatically.

Commands change state:

```text
CreateProduct
ChangePrice
UpdateAvailability
ActivateSeller
```

Queries read state:

```text
GetProduct
SearchProducts
GetCurrentPrice
GetAvailability
```

CQRS does not require separate physical databases for every use case.

The separation is primarily conceptual and architectural.

## 18. Read Models

High-volume reads may use specialized projections.

Examples:

```text
Elasticsearch product projection
Redis availability projection
Customer commerce projection
```

Read models may be denormalized.

They are not automatically sources of truth.

## 19. Domain Events

Domain Events represent meaningful facts inside a Bounded Context.

Examples:

```text
ProductCreatedDomainEvent
PriceChangedDomainEvent
AvailabilityChangedDomainEvent
```

Domain Events remain internal unless explicitly translated into Integration Events.

## 20. Integration Events

Integration Events cross context/process boundaries.

Examples:

```text
ProductCreated
PriceChanged
AvailabilityChanged
```

They must not expose internal Aggregate structures.

## 21. AI Boundary

Generative AI is an external capability from the perspective of commerce Domains.

Correct:

```text
Catalog Application
       │
       ▼
IGenerativeAiProvider
       │
       ├── Azure AI Adapter
       └── Google AI Adapter
```

Forbidden:

```text
Catalog Domain
       │
       ▼
Azure AI SDK
```

AI output must pass through Application and Domain validation before becoming canonical state.

## 22. Persistence Boundary

Persistence technology must not dictate Domain design.

The architecture may use:

```text
MongoDB
SQL Server / PostgreSQL / Azure SQL
Redis
Elasticsearch
Object Storage
```

without exposing those technologies to Domain code.

## 23. Framework Independence

Frameworks are replaceable implementation tools.

Yunu.Commerce may use:

```text
ASP.NET Core
Entity Framework Core
Dapper
MediatR or equivalent
Polly / Microsoft resilience libraries
MongoDB.Driver
```

but the architecture must not become defined by those libraries.

## 24. Modular Monolith First

Yunu.Commerce may begin as a Modular Monolith with independently structured Bounded Contexts.

Conceptually:

```text
Yunu.Commerce
│
├── Catalog
├── Sellers
├── Offers
├── Pricing
├── Availability
├── Fulfillment
└── Freight
```

This avoids premature distributed-system complexity while preserving extraction boundaries.

## 25. Future Microservice Extraction

A context may later become an independently deployed service when justified by:

```text
independent scaling
fault isolation
deployment cadence
team ownership
workload characteristics
technology requirements
```

The Domain should not need redesign merely because deployment topology changes.

## 26. Consequences

### Positive

This decision provides:

```text
clear business boundaries
testable Domain logic
replaceable infrastructure
cloud/provider independence
controlled dependencies
easier integration testing
safer AI adoption
future microservice extraction
better long-term maintainability
```

### Negative

This decision introduces:

```text
more projects
more interfaces
mapping between layers
additional architectural discipline
more explicit contracts
higher initial setup cost
```

These costs are accepted because the platform is expected to contain substantial business and integration complexity.

## 27. Alternatives Considered

### Traditional Layered Architecture

Example:

```text
Controller
   ↓
Service
   ↓
Repository
   ↓
Database
```

Rejected as the primary architecture because it often becomes data-centric and does not sufficiently protect complex Domain boundaries.

### CRUD-Centric Architecture

Rejected because Yunu.Commerce contains business behavior, multiple contexts and complex integrations that exceed simple CRUD requirements.

### Microservices First

Rejected as a mandatory starting topology.

Microservices remain an evolution option, not an initial requirement.

### Provider-Centric Architecture

Example:

```text
Azure services define application structure
```

Rejected because cloud technology must remain replaceable around the business core.

## 28. Architecture Enforcement

Architecture rules should be enforced through:

```text
project references
architecture tests
code review
Copilot instructions
ADRs
CI checks
```

Documentation alone is insufficient.

## 29. Copilot Rules

GitHub Copilot must follow these rules when generating Yunu.Commerce code:

```text
Respect Bounded Context boundaries.

Do not reference Infrastructure from Domain.

Do not reference provider SDKs from Domain.

Do not reference another Bounded Context's Domain project.

Use Ports before Adapters.

Keep controllers/endpoints thin.

Keep business invariants inside Domain.

Use Application for orchestration.

Use Infrastructure for persistence and external providers.

Do not create generic abstractions without a concrete need.

Do not create cross-context database joins.

Do not bypass Domain behavior with direct persistence updates.

Do not allow AI-generated data to bypass validation.

Do not introduce microservices solely because DDD is being used.
```

## 30. Example Dependency Structure

```text
Yunu.Commerce.Catalog.Api
        │
        ├───────────────┐
        ▼               ▼
Catalog.Application   Catalog.Infrastructure
        │               │
        ▼               │
   Catalog.Domain ◄──────┘
```

Infrastructure may depend on Domain/Application contracts as required.

Domain depends on neither.

## 31. Example Complete Flow

```text
HTTP Request
     │
     ▼
Catalog API
     │
     ▼
Application Command
     │
     ▼
Product Aggregate
     │
     ▼
Repository Port
     │
     ▼
MongoDB Adapter
     │
     ▼
Outbox
     │
     ▼
Kafka
     │
     ▼
Search Projection Worker
     │
     ▼
Elasticsearch
```

The business model remains isolated from every technology shown below the ports.

## 32. Relationship to Other ADRs

This ADR establishes the architectural foundation for:

```text
ADR-0002 - Bounded Context Strategy

ADR-0003 - Database per Bounded Context

ADR-0004 - Kafka for Event-Driven Integration

ADR-0005 - Transactional Outbox

ADR-0006 - Redis for Distributed Cache

ADR-0007 - Elasticsearch for Search Projections

ADR-0008 - GenAI Provider Abstraction

ADR-0009 - Cloud Provider Strategy
```

Those ADRs must comply with the dependency and ownership rules defined here.

## 33. Final Decision

Yunu.Commerce adopts:

```text
DDD
+
Clean Architecture
+
Hexagonal Architecture
+
Event-Driven Architecture
```

as complementary architectural approaches.

DDD protects the business model.

Clean Architecture protects dependency direction.

Hexagonal Architecture protects the business from external technologies.

EDA protects Bounded Context autonomy during asynchronous integration.

The architecture is designed so that:

> Business rules survive changes in databases, messaging platforms, cloud providers, AI providers, frameworks and deployment topology.
