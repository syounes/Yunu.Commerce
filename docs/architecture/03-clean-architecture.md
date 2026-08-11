# Yunu.Commerce - Clean Architecture

## 1. Purpose

This document defines the Clean Architecture rules for Yunu.Commerce.

The objective is to keep business logic independent from infrastructure, frameworks, delivery mechanisms and external providers.

The architecture must allow:

* business rules to evolve independently
* infrastructure technologies to be replaced
* APIs and workers to remain thin
* domain behavior to be tested without infrastructure
* bounded contexts to remain isolated
* deployment topology to evolve without redesigning the domain

The fundamental rule is:

> Dependencies point inward toward the business core.

---

# 2. Layer Model

Each business Bounded Context should follow the conceptual structure:

```text
┌──────────────────────────────┐
│        API / Worker          │
│     Delivery / Hosting       │
└──────────────┬───────────────┘
               │
               ▼
┌──────────────────────────────┐
│         Application          │
│ Use Cases / Commands / Query │
└──────────────┬───────────────┘
               │
               ▼
┌──────────────────────────────┐
│            Domain            │
│   Business Model / Rules     │
└──────────────────────────────┘

Infrastructure implements ports required by Application and Domain.
```

Infrastructure is an outer layer.

It depends on internal abstractions.

Internal layers must never depend on Infrastructure.

---

# 3. Dependency Direction

The allowed conceptual dependency direction is:

```text
API / Worker
     │
     ▼
Application
     │
     ▼
Domain

Infrastructure
     │
     ├────────► Application
     │
     └────────► Domain
```

Infrastructure may reference Application and Domain because it implements their required contracts.

Application may reference Domain.

Domain must not reference Application, Infrastructure or hosting projects.

---

# 4. Project Structure Per Bounded Context

A typical Bounded Context may contain:

```text
Yunu.Commerce.Catalog.Domain
Yunu.Commerce.Catalog.Application
Yunu.Commerce.Catalog.Infrastructure
Yunu.Commerce.Catalog.Contracts
Yunu.Commerce.Catalog.Api
Yunu.Commerce.Catalog.Worker
```

Not every context requires every project.

Projects must be created only when there is a real architectural responsibility.

For example, a context without background processing does not automatically require a Worker.

---

# 5. Domain Layer

## Responsibility

Domain contains the business model and business behavior.

It must represent the business language of the Bounded Context.

Potential contents:

```text
Aggregates
Aggregate Roots
Entities
Value Objects
Domain Services
Domain Events
Specifications
Business Rules
Domain Exceptions
Repository Contracts
Domain Policies
Domain Factories
```

The Domain must remain as framework-independent as practical.

---

# 6. Domain Dependency Rules

A Domain project may depend only on:

* the .NET Base Class Library
* carefully selected internal shared abstractions
* packages that do not introduce infrastructure coupling and are explicitly justified

The Domain must not reference:

```text
ASP.NET Core
Entity Framework Core
MongoDB.Driver
Dapper
Kafka clients
Redis clients
Elasticsearch clients
Azure SDKs
Google Cloud SDKs
OpenAI SDKs
HTTP clients
filesystem implementations
logging providers
dependency injection containers
```

Domain must not know how data is persisted.

Domain must not know how messages are published.

Domain must not know whether execution happens through HTTP, Kafka, CLI, tests or background workers.

---

# 7. Domain Behavior

Entities and Aggregates must contain business behavior.

Avoid anemic models where business state is freely mutated from outside.

Undesirable:

```csharp
product.Status = ProductStatus.Active;
product.Name = name;
```

Preferred:

```csharp
product.Rename(name);
product.Activate();
```

The behavior method must enforce the relevant invariant.

Setters should not be publicly exposed unless there is a strong domain reason.

---

# 8. Aggregate Responsibilities

An Aggregate is responsible for maintaining consistency inside its boundary.

An Aggregate Root:

* protects invariants
* controls changes to internal entities
* exposes business operations
* may raise Domain Events
* defines a transactional consistency boundary

External code should not directly mutate internal Aggregate entities.

Strong consistency should generally remain within the Aggregate boundary.

---

# 9. Repository Contracts

Repository abstractions may exist in Domain when persistence is required to reconstitute Aggregates.

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

The interface expresses a Domain persistence need.

It must not expose infrastructure-specific concepts.

Forbidden:

```csharp
Task<IMongoCollection<ProductDocument>> GetCollectionAsync();
```

Forbidden:

```csharp
IQueryable<Product> Query();
```

when that leaks persistence behavior into the Domain.

Implementation belongs to Infrastructure.

---

# 10. Domain Services

Domain Services are appropriate when important business behavior:

* does not naturally belong to one Entity or Value Object
* operates on domain concepts
* represents domain knowledge
* remains infrastructure-independent

Example conceptual candidates:

```text
ProductClassificationPolicy
PriceEligibilityPolicy
AvailabilityPolicy
OfferActivationPolicy
```

Do not create Domain Services merely to move code out of Entities.

Prefer behavior inside the relevant Aggregate when ownership is clear.

---

# 11. Domain Events

Domain Events represent meaningful facts that occurred inside a Bounded Context.

Examples:

```text
ProductCreatedDomainEvent
SkuActivatedDomainEvent
OfferActivatedDomainEvent
PriceChangedDomainEvent
```

Domain Events must not depend on Kafka or any transport technology.

A Domain Event is not the same thing as an Integration Event.

Translation from Domain Event to Integration Event occurs outside the Domain.

---

# 12. Application Layer

## Responsibility

Application contains use-case orchestration.

Application answers:

> What must happen to execute this system operation?

Application coordinates Domain behavior and external ports.

It must not contain core business rules that belong to Domain.

---

# 13. Application Contents

The Application layer may contain:

```text
Commands
Command Handlers
Queries
Query Handlers
Use Cases
Application Services
DTOs
Validators
Ports
Interfaces
Authorization Rules
Mapping
Pipeline Behaviors
Transaction Abstractions
Integration Event Coordination
```

---

# 14. Application Flow

Typical command flow:

```text
API / Worker
     │
     ▼
Command
     │
     ▼
Command Handler
     │
     ├── Load Aggregate
     │
     ├── Call Domain Behavior
     │
     ├── Persist Aggregate
     │
     └── Coordinate Integration
     ▼
Result
```

Application coordinates.

Domain decides.

---

# 15. Example Responsibility Split

Example: activating a SKU.

Application Handler:

```text
Receive ActivateSkuCommand
Load Product Aggregate
Call product.ActivateSku(...)
Persist Product
Commit transaction
```

Domain:

```text
Determine whether SKU can be activated
Validate business invariants
Change state
Raise Domain Event
```

Infrastructure:

```text
Load and save aggregate using MongoDB
Persist Outbox message
Publish Kafka event later
```

API:

```text
Map HTTP request to command
Return HTTP response
```

Each layer has one responsibility.

---

# 16. CQRS in Application

Commands modify system state.

Queries retrieve information.

Command examples:

```text
CreateProductCommand
UpdateProductCommand
CreateSkuCommand
ActivateSkuCommand
CreateOfferCommand
ChangePriceCommand
UpdateAvailabilityCommand
```

Query examples:

```text
GetProductQuery
GetSkuQuery
SearchProductsQuery
GetOfferQuery
GetCurrentPriceQuery
GetAvailabilityQuery
```

CQRS does not require physically separate databases.

It represents separation of intent.

Read models may later use specialized stores when beneficial.

---

# 17. Command Rules

Commands should express user or system intent.

Prefer:

```text
CreateProductCommand
ActivateSkuCommand
ChangeRegionalPriceCommand
```

Avoid commands named after technical persistence operations:

```text
InsertProductRowCommand
UpdateMongoProductCommand
```

Commands must speak the application language.

---

# 18. Query Rules

Queries must not accidentally become state-changing operations.

A Query should be side-effect free from a business perspective.

Query handlers may read from:

* repositories
* optimized projections
* search indexes
* read databases
* cache

depending on architectural requirements.

Queries do not need to reconstruct complete Aggregates unless Domain behavior is required.

---

# 19. Application Ports

Application may define abstractions for capabilities it needs.

Examples:

```csharp
public interface IEventPublisher
{
    Task PublishAsync<T>(
        T integrationEvent,
        CancellationToken cancellationToken);
}
```

```csharp
public interface ICacheProvider
{
    Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken);
}
```

```csharp
public interface IGenerativeAIProvider
{
    Task<AIResponse> GenerateAsync(
        AIRequest request,
        CancellationToken cancellationToken);
}
```

Infrastructure implements these ports.

The Application layer must not depend on provider implementations.

---

# 20. Infrastructure Layer

## Responsibility

Infrastructure implements technical capabilities.

Typical responsibilities include:

```text
MongoDB persistence
Relational persistence
Kafka messaging
Redis caching
Elasticsearch indexing
Azure OpenAI integration
Google Vertex AI integration
HTTP clients
External APIs
Object storage
Secrets integration
Observability exporters
```

Infrastructure translates technical mechanisms into internal ports.

---

# 21. Infrastructure Implementations

Examples:

```text
MongoProductRepository
SqlPriceRepository
RedisCacheProvider
KafkaEventPublisher
ElasticSearchIndexer
AzureOpenAIProvider
GoogleVertexAIProvider
ExternalFreightProviderAdapter
```

These classes may depend on vendor SDKs.

Their vendor-specific types must not cross into Application or Domain contracts.

---

# 22. Infrastructure Mapping

Persistence models may differ from Domain models.

Example:

```text
Product Aggregate
      │
      ▼
ProductPersistenceMapper
      │
      ▼
ProductDocument
      │
      ▼
MongoDB
```

The Domain Aggregate does not need database annotations unless explicitly justified.

Infrastructure-specific persistence concerns should remain in Infrastructure.

---

# 23. API Layer

## Responsibility

API is a delivery mechanism.

API should be thin.

It may contain:

```text
Endpoints / Controllers
Request Models
Response Models
Authentication configuration
Authorization configuration
API versioning
HTTP-specific mapping
OpenAPI configuration
Middleware
Dependency composition
```

API must not contain Domain business rules.

---

# 24. API Request Flow

Typical API flow:

```text
HTTP Request
     │
     ▼
Endpoint
     │
     ▼
Application Command / Query
     │
     ▼
Application Handler
     │
     ▼
Domain / Read Infrastructure
     │
     ▼
Application Result
     │
     ▼
HTTP Response
```

HTTP status codes are delivery concerns.

The Domain must not know about HTTP.

---

# 25. Worker Layer

Workers are also delivery mechanisms.

Potential worker responsibilities:

```text
Kafka consumers
Outbox publishing
Scheduled processing
Search indexing
AI enrichment
Data synchronization
Background integration
```

Workers orchestrate Application use cases.

They must not become containers for business rules.

---

# 26. Contracts Layer

A Contracts project may contain externally visible contracts such as:

```text
Integration Events
Public API DTO contracts
Messages
Versioned event schemas
Cross-process contracts
```

Contracts must not expose internal Domain implementation details unnecessarily.

A Domain Entity must not automatically become an API contract.

---

# 27. Contracts Independence

Public contracts evolve independently from Domain internals.

Example:

```text
Catalog.Domain.Product
```

may contain internal behavior and structure that should never appear in:

```text
ProductCreatedIntegrationEventV1
```

Contract design must prioritize consumer compatibility.

---

# 28. Dependency Matrix

The intended project reference rules are:

| Project        | May Reference                                |
| -------------- | -------------------------------------------- |
| Domain         | SharedKernel only when justified             |
| Application    | Domain, approved BuildingBlocks              |
| Infrastructure | Application, Domain, approved BuildingBlocks |
| Contracts      | minimal BuildingBlocks only                  |
| API            | Application, Infrastructure, Contracts       |
| Worker         | Application, Infrastructure, Contracts       |
| Tests          | target project plus test infrastructure      |

No project may reference another context's Infrastructure project.

---

# 29. Forbidden Dependencies

Examples of forbidden project references:

```text
Catalog.Domain
    → Catalog.Infrastructure
```

```text
Catalog.Application
    → Catalog.Infrastructure
```

```text
Pricing.Domain
    → Catalog.Domain
```

```text
Pricing.Infrastructure
    → Catalog.Infrastructure
```

```text
Catalog.Domain
    → ASP.NET Core
```

```text
Catalog.Domain
    → MongoDB.Driver
```

Cross-context communication must use explicit contracts, IDs, application APIs or integration events.

---

# 30. Dependency Injection

Dependency Injection is configured at the composition boundary.

Typically:

```text
API / Worker
      │
      ▼
AddApplication()
AddInfrastructure()
```

Domain must not perform service resolution.

Forbidden:

```csharp
serviceProvider.GetRequiredService<IProductRepository>();
```

inside Domain or Application business logic.

Use constructor injection.

---

# 31. Composition Root

Each executable host is responsible for assembling its dependencies.

Example:

```csharp
builder.Services
    .AddCatalogApplication()
    .AddCatalogInfrastructure(builder.Configuration);
```

Infrastructure registration extensions may exist to keep host startup readable.

The host chooses implementations.

Inner layers define requirements.

---

# 32. Configuration

Configuration belongs to outer layers.

Examples:

```text
MongoOptions
KafkaOptions
RedisOptions
ElasticOptions
AzureOpenAIOptions
GoogleAIOptions
```

Strongly typed Options should be used where practical.

The Domain must not read:

```text
appsettings.json
environment variables
Azure Key Vault
Kubernetes secrets
```

---

# 33. Transactions

Application coordinates transactional use cases.

Transaction abstractions may be defined by Application when required.

Infrastructure supplies concrete implementations.

A transaction boundary should generally align with an Aggregate consistency boundary.

Distributed transactions across Bounded Contexts must be avoided.

Use asynchronous integration and eventual consistency instead.

---

# 34. Outbox Responsibility

The Domain may raise a Domain Event.

Application and Infrastructure coordinate reliable publication.

Conceptual flow:

```text
Domain
   │
   │ Domain Event
   ▼
Application
   │
   ▼
Persistence Transaction
   │
   ├── Aggregate
   └── Outbox Message
          │
          ▼
Infrastructure Worker
          │
          ▼
Kafka
```

Kafka-specific concepts must not enter Domain Events.

---

# 35. Validation

Validation has multiple levels.

## Input Validation

Application/API may validate:

```text
required fields
format
length
basic request structure
```

## Business Validation

Domain validates:

```text
business invariants
valid state transitions
domain-specific rules
```

Do not duplicate core business rules exclusively in API validators.

API validation cannot be the only protection of a Domain invariant.

---

# 36. Error Translation

Domain errors are business concerns.

HTTP errors are transport concerns.

Example:

```text
Domain
ProductCannotBeActivated
        │
        ▼
Application
Domain outcome
        │
        ▼
API
409 Conflict
```

The Domain must not throw HTTP-specific exceptions.

---

# 37. Time

Time-sensitive business logic must be testable.

Do not scatter calls to:

```csharp
DateTime.UtcNow
```

through Domain behavior when deterministic testing requires time abstraction.

Prefer a controlled time source where business rules depend on the current time.

System timestamps crossing process boundaries should use UTC.

Prefer `DateTimeOffset`.

---

# 38. Persistence Ignorance

Domain modeling must not be distorted merely to satisfy a database driver.

Do not design Aggregates around:

```text
MongoDB document limitations
EF Core convenience
Elasticsearch mapping behavior
```

Infrastructure adapts persistence to the Domain.

Pragmatic exceptions may exist, but they require deliberate architectural reasoning.

---

# 39. Logging

Domain should generally not depend on logging infrastructure.

Application and Infrastructure may perform structured logging for:

```text
Use case execution
External calls
Persistence
Messaging
Failures
Performance
```

Do not use logging as a substitute for Domain Events.

---

# 40. Observability Boundaries

Tracing may start in API or Worker hosts.

Context should flow through Application and Infrastructure.

Business APIs should not require vendor-specific tracing types.

Prefer OpenTelemetry-compatible instrumentation in outer layers.

---

# 41. Testing by Layer

## Domain Tests

Must test business behavior without infrastructure.

```text
No MongoDB
No Kafka
No Redis
No Azure
No HTTP server
```

## Application Tests

Test use-case orchestration with mocked/fake ports where appropriate.

## Infrastructure Tests

Test real infrastructure adapters.

Testcontainers may be used for:

```text
MongoDB
SQL
Kafka
Redis
Elasticsearch
```

## API Tests

Validate:

```text
routing
serialization
authentication behavior
status mapping
end-to-end use cases
```

---

# 42. Architecture Tests

Architecture rules must be executable wherever practical.

Tests should verify rules such as:

```text
Domain does not depend on Infrastructure
Domain does not depend on ASP.NET Core
Application does not depend on Infrastructure
Domain does not depend on MongoDB.Driver
Domain does not depend on EF Core
Bounded Contexts do not reference foreign Infrastructure projects
```

Architecture violations should fail CI.

---

# 43. Package Management

Central package management should be preferred.

The repository may use:

```text
Directory.Packages.props
```

for package versions.

Shared build rules may use:

```text
Directory.Build.props
```

This reduces version drift across projects.

---

# 44. Nullable Reference Types

Nullable reference types must be enabled.

Example shared configuration:

```xml
<Nullable>enable</Nullable>
```

New warnings must not be ignored by default.

Fix the model rather than using unnecessary null-forgiving operators.

---

# 45. Implicit Usings

Implicit usings may be enabled when consistent with the codebase.

Explicit imports should be used when they improve clarity.

No architecture rule should depend on compiler convenience features.

---

# 46. Async I/O

All infrastructure I/O must use asynchronous APIs where available.

Application asynchronous methods should normally accept:

```csharp
CancellationToken cancellationToken
```

Avoid synchronous blocking:

```csharp
.Result
.Wait()
.GetAwaiter().GetResult()
```

inside asynchronous application paths.

---

# 47. Mapping

Mapping must happen at boundaries.

Examples:

```text
API Request
   ↓
Command
```

```text
Domain Aggregate
   ↓
Persistence Model
```

```text
External ERP Model
   ↓
Canonical Integration Contract
```

Avoid massive global mapping configurations that hide important transformations.

Mapping code should remain understandable.

---

# 48. Shared Kernel

The Shared Kernel must remain minimal.

Do not move a concept to Shared Kernel simply because two projects use similar structures.

Shared code increases coupling.

Allowed additions require architectural justification.

Potential technical building blocks may include:

```text
base Domain Event abstraction
result primitives
correlation primitives
guard abstractions
```

even these should be added carefully.

---

# 49. Cross-Context References

A context must not reference another context's Domain assembly for convenience.

Example:

Forbidden:

```text
Pricing.Application
    → Catalog.Domain
```

Preferred:

```text
Pricing
uses SkuId through its own representation
or explicit integration/application contract
```

Bounded Context independence takes priority over type reuse.

---

# 50. Integration Boundaries

External system integration belongs outside core business layers.

For example:

```text
SAP
  │
  ▼
Integrations.Infrastructure
  │
  ▼
Canonical Contract
  │
  ▼
Catalog Application
```

External models must not become Domain Entities.

---

# 51. AI Boundaries

AI provider SDKs belong in Infrastructure.

Example:

```text
AI.Application
     │
     ▼
IGenerativeAIProvider
     │
     ▼
AI.Infrastructure
     │
     ├── AzureOpenAIProvider
     └── GoogleVertexAIProvider
```

AI Domain/Application must not expose Azure or Google SDK types.

---

# 52. Search Boundaries

Elasticsearch is Infrastructure.

Application may define abstractions such as:

```text
IProductSearch
IProductIndexer
```

Infrastructure implements them using Elasticsearch.

Do not expose Elasticsearch query types through Application contracts.

---

# 53. Cache Boundaries

Redis is Infrastructure.

Application may depend on a cache abstraction when justified.

Do not expose:

```text
IDatabase
RedisKey
RedisValue
```

outside Infrastructure.

Cache must remain replaceable.

---

# 54. Messaging Boundaries

Kafka is Infrastructure.

Application and Contracts operate with internal abstractions and integration messages.

Do not expose Kafka concepts such as:

```text
ConsumeResult
TopicPartition
ProducerBuilder
```

outside messaging infrastructure.

---

# 55. API Model Separation

Do not bind Domain Entities directly as HTTP request models.

Avoid:

```csharp
Post(Product product)
```

Prefer:

```csharp
Post(CreateProductRequest request)
```

then translate to an Application command.

This prevents external contracts from controlling internal Domain structure.

---

# 56. Application Result Model

Application results should remain transport-neutral.

They should not contain:

```text
HTTP status codes
ASP.NET IActionResult
HTTP headers
```

The API layer translates application outcomes into transport responses.

---

# 57. Minimal APIs vs Controllers

The choice between Minimal APIs and Controllers is a delivery implementation decision.

It must not affect Domain or Application design.

Whichever style is selected:

* endpoints remain thin
* use cases remain in Application
* business rules remain in Domain

The final choice should be documented in an ADR if it materially affects the platform standard.

---

# 58. Modularity

Clean Architecture applies inside each Bounded Context.

The solution should not create one global:

```text
Yunu.Commerce.Domain
Yunu.Commerce.Application
Yunu.Commerce.Infrastructure
```

containing all business areas.

Preferred:

```text
Catalog.Domain
Catalog.Application
Catalog.Infrastructure

Pricing.Domain
Pricing.Application
Pricing.Infrastructure
```

This preserves Bounded Context boundaries.

---

# 59. Deployment Independence

Project structure should support future independent deployment without requiring Domain redesign.

However, projects should not be created solely because they might one day become microservices.

Architecture must remain modular-first and deployment-flexible.

---

# 60. Clean Architecture Decision Checklist

Before adding code, verify:

```text
What Bounded Context owns this behavior?

What layer owns this responsibility?

Does this dependency point inward?

Is this business logic or infrastructure logic?

Does the Domain know too much about technology?

Does Application know too much about transport?

Does API contain business behavior?

Is vendor-specific code leaking inward?

Could this infrastructure implementation be replaced?
```

If the answer reveals layer leakage, redesign before implementation.

---

# 61. Core Rule

The central Clean Architecture rule for Yunu.Commerce is:

> Domain represents business truth.
> Application orchestrates use cases.
> Infrastructure implements technology.
> Hosts deliver the system.

No outer technology may become a requirement of the core business model.
