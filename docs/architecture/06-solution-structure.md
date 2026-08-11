# Yunu.Commerce - Solution Structure

## 1. Purpose

This document defines the physical .NET solution structure for Yunu.Commerce.

It translates the previously defined:

- Domain-Driven Design
- Clean Architecture
- Hexagonal Architecture
- Event-Driven Architecture
- Bounded Contexts

into concrete .NET projects, folders and project references.

The objective is to create a modular, compiling solution skeleton before detailed Domain implementation begins.

The initial solution must provide strong module boundaries while avoiding premature microservice deployment complexity.

---

# 2. Solution Name

The root solution is:

```text
Yunu.Commerce.sln
```

The root namespace is:

```text
Yunu.Commerce
```

Project naming convention:

```text
Yunu.Commerce.{Module}.{Layer}
```

Examples:

```text
Yunu.Commerce.Catalog.Domain
Yunu.Commerce.Catalog.Application
Yunu.Commerce.Catalog.Infrastructure
Yunu.Commerce.Catalog.Contracts
```

---

# 3. Repository Structure

The repository root must follow this structure:

```text
Yunu.Commerce/
│
├── .github/
│   └── copilot-instructions.md
│
├── docs/
│   ├── architecture/
│   ├── adr/
│   ├── domains/
│   ├── ai/
│   ├── data/
│   └── integration/
│
├── scripts/
│
├── src/
│   ├── BuildingBlocks/
│   ├── Modules/
│   └── Hosts/
│
├── tests/
│   ├── Architecture/
│   ├── Unit/
│   └── Integration/
│
├── deploy/
│   ├── docker/
│   ├── kubernetes/
│   └── helm/
│
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── .editorconfig
├── .gitignore
├── README.md
└── Yunu.Commerce.sln
```

---

# 4. High-Level Solution Organization

The .NET solution should conceptually contain:

```text
Yunu.Commerce

├── BuildingBlocks
│
├── Modules
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
├── Hosts
│
└── Tests
```

The physical repository folders should reflect these boundaries.

---

# 5. Modular-First Deployment Strategy

A Bounded Context does not automatically become an independent executable.

The initial runtime topology is:

```text
                 Yunu.Commerce.Api
                        │
                        ▼
          ┌──────────────────────────┐
          │     Commerce Modules     │
          │                          │
          │ Catalog                  │
          │ Sellers                  │
          │ Offers                   │
          │ Pricing                  │
          │ Availability             │
          │ Fulfillment              │
          │ Freight                  │
          │ Search                   │
          │ AI                       │
          └──────────────────────────┘


               Yunu.Commerce.Worker
                        │
                        ▼
          ┌──────────────────────────┐
          │ Background Processing    │
          │                          │
          │ Kafka Consumers          │
          │ Outbox Processing        │
          │ Search Indexing          │
          │ AI Enrichment            │
          │ Integrations             │
          └──────────────────────────┘
```

This creates a modular distributed-ready architecture without requiring one microservice per Bounded Context immediately.

---

# 6. Hosts

Initial executable hosts:

```text
src/Hosts/

├── Yunu.Commerce.Api
└── Yunu.Commerce.Worker
```

Future hosts may be created when operational requirements justify them.

Examples:

```text
Yunu.Commerce.Catalog.Api
Yunu.Commerce.Search.Worker
Yunu.Commerce.AI.Worker
```

Such extraction must not require redesigning the internal Domain.

---

# 7. API Host

Project:

```text
Yunu.Commerce.Api
```

Responsibilities:

- ASP.NET Core hosting
- dependency composition
- authentication
- authorization
- API versioning
- HTTP endpoints
- OpenAPI
- middleware
- health checks
- observability bootstrap
- module registration

The API host must not contain business rules.

---

# 8. Worker Host

Project:

```text
Yunu.Commerce.Worker
```

Responsibilities:

- Kafka consumers
- Outbox processors
- Inbox processors
- search indexing
- AI enrichment processing
- integration processing
- scheduled background tasks
- health checks
- observability bootstrap

The Worker host must not contain Domain business rules.

It invokes module Application layers.

---

# 9. Building Blocks

Physical location:

```text
src/BuildingBlocks/
```

Initial Building Block projects:

```text
Yunu.Commerce.SharedKernel
Yunu.Commerce.Contracts
Yunu.Commerce.EventBus
Yunu.Commerce.Observability
Yunu.Commerce.Security
```

Building Blocks must remain small and focused.

They must not become a global dumping ground.

---

# 10. SharedKernel

Project:

```text
Yunu.Commerce.SharedKernel
```

Possible responsibilities:

- minimal base Domain Event abstraction
- selected domain primitives
- result primitives if truly shared
- guard abstractions
- strongly justified technical primitives

SharedKernel must not contain:

```text
Product
SKU
Seller
Offer
Price
Availability
```

because those concepts belong to specific Bounded Contexts.

---

# 11. Global Contracts

Project:

```text
Yunu.Commerce.Contracts
```

This project should contain only genuinely platform-level technical contracts.

Examples may eventually include:

```text
IntegrationEventEnvelope
Correlation metadata
Message metadata
```

Business-specific event contracts should remain within their owning module.

---

# 12. EventBus

Project:

```text
Yunu.Commerce.EventBus
```

Responsibility:

Provide messaging abstractions and reusable event infrastructure.

Potential concepts:

```text
IIntegrationEventPublisher
IntegrationEventEnvelope
Event metadata
Messaging registration abstractions
```

Kafka-specific implementations may live here only when the project is explicitly infrastructure-oriented.

Domain modules must never reference Kafka packages directly.

---

# 13. Observability Building Block

Project:

```text
Yunu.Commerce.Observability
```

Responsibilities may include reusable configuration for:

```text
OpenTelemetry
Tracing
Metrics
Structured logging enrichment
Correlation propagation
```

It must not contain business-specific telemetry logic.

---

# 14. Security Building Block

Project:

```text
Yunu.Commerce.Security
```

Responsibilities may include reusable technical abstractions for:

```text
Authentication context
Authorization context
Current principal abstractions
Security registration
```

Microsoft Entra ID and other provider-specific configuration remain outer infrastructure concerns.

---

# 15. Modules Root

All business modules live under:

```text
src/Modules/
```

Initial modules:

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

---

# 16. Catalog Module

Physical structure:

```text
src/Modules/Catalog/

├── Yunu.Commerce.Catalog.Domain
├── Yunu.Commerce.Catalog.Application
├── Yunu.Commerce.Catalog.Infrastructure
└── Yunu.Commerce.Catalog.Contracts
```

Catalog owns:

```text
Product
SKU
Category
Brand
Attributes
Specifications
Media
```

Detailed Domain implementation will be introduced later.

During skeleton creation, do not fabricate Product Aggregate behavior.

---

# 17. Sellers Module

```text
src/Modules/Sellers/

├── Yunu.Commerce.Sellers.Domain
├── Yunu.Commerce.Sellers.Application
├── Yunu.Commerce.Sellers.Infrastructure
└── Yunu.Commerce.Sellers.Contracts
```

Sellers owns:

```text
Seller
Merchant
SellerType
SellerStatus
```

---

# 18. Offers Module

```text
src/Modules/Offers/

├── Yunu.Commerce.Offers.Domain
├── Yunu.Commerce.Offers.Application
├── Yunu.Commerce.Offers.Infrastructure
└── Yunu.Commerce.Offers.Contracts
```

Offers owns:

```text
Offer
OfferStatus
Commercial offer relationships
```

---

# 19. Pricing Module

```text
src/Modules/Pricing/

├── Yunu.Commerce.Pricing.Domain
├── Yunu.Commerce.Pricing.Application
├── Yunu.Commerce.Pricing.Infrastructure
└── Yunu.Commerce.Pricing.Contracts
```

Pricing owns:

```text
National Price
Regional Price
Payment Price
PIX Price
Boleto Price
Credit Card conditions
Installments
Promotional pricing
```

---

# 20. Availability Module

```text
src/Modules/Availability/

├── Yunu.Commerce.Availability.Domain
├── Yunu.Commerce.Availability.Application
├── Yunu.Commerce.Availability.Infrastructure
└── Yunu.Commerce.Availability.Contracts
```

Availability owns:

```text
Availability
Regional Availability
Branch Availability
Stock Position
```

---

# 21. Fulfillment Module

```text
src/Modules/Fulfillment/

├── Yunu.Commerce.Fulfillment.Domain
├── Yunu.Commerce.Fulfillment.Application
├── Yunu.Commerce.Fulfillment.Infrastructure
└── Yunu.Commerce.Fulfillment.Contracts
```

Fulfillment owns:

```text
FulfillmentNode
Branch
Store
Warehouse
Distribution Center
Fulfillment capabilities
```

---

# 22. Freight Module

```text
src/Modules/Freight/

├── Yunu.Commerce.Freight.Domain
├── Yunu.Commerce.Freight.Application
├── Yunu.Commerce.Freight.Infrastructure
└── Yunu.Commerce.Freight.Contracts
```

Freight owns:

```text
FreightQuote
FreightOption
DeliveryMethod
Carrier
ServiceLevel
DeliveryPromise
```

---

# 23. Search Module

Search is primarily read-oriented.

Initial structure:

```text
src/Modules/Search/

├── Yunu.Commerce.Search.Application
├── Yunu.Commerce.Search.Infrastructure
└── Yunu.Commerce.Search.Contracts
```

A Search Domain project must not be created unless meaningful Search business rules emerge.

Initial Search Infrastructure will eventually contain:

```text
Elasticsearch indexing
Elasticsearch querying
Vector search adapters
```

---

# 24. AI Module

Initial structure:

```text
src/Modules/AI/

├── Yunu.Commerce.AI.Application
├── Yunu.Commerce.AI.Infrastructure
└── Yunu.Commerce.AI.Contracts
```

A dedicated AI Domain project should be added only if AI orchestration develops meaningful domain behavior requiring one.

Initial AI responsibilities include:

```text
Generative AI provider abstraction
AI model routing
Catalog enrichment orchestration
Embeddings
RAG
Agent tool orchestration
```

Provider implementations eventually include:

```text
Azure OpenAI
Google Vertex AI
```

---

# 25. Integrations Module

Initial structure:

```text
src/Modules/Integrations/

├── Yunu.Commerce.Integrations.Application
├── Yunu.Commerce.Integrations.Infrastructure
└── Yunu.Commerce.Integrations.Contracts
```

Responsibilities include:

```text
Anti-Corruption Layers
External API adapters
ERP integrations
OMS integrations
WMS integrations
Marketplace integrations
Import pipelines
Export pipelines
```

External models must never leak into business Domain projects.

---

# 26. Full Initial Source Tree

The initial source structure is therefore:

```text
src/

├── BuildingBlocks/
│
│   ├── Yunu.Commerce.SharedKernel/
│   ├── Yunu.Commerce.Contracts/
│   ├── Yunu.Commerce.EventBus/
│   ├── Yunu.Commerce.Observability/
│   └── Yunu.Commerce.Security/
│
├── Modules/
│
│   ├── Catalog/
│   │   ├── Yunu.Commerce.Catalog.Domain/
│   │   ├── Yunu.Commerce.Catalog.Application/
│   │   ├── Yunu.Commerce.Catalog.Infrastructure/
│   │   └── Yunu.Commerce.Catalog.Contracts/
│   │
│   ├── Sellers/
│   │   ├── Yunu.Commerce.Sellers.Domain/
│   │   ├── Yunu.Commerce.Sellers.Application/
│   │   ├── Yunu.Commerce.Sellers.Infrastructure/
│   │   └── Yunu.Commerce.Sellers.Contracts/
│   │
│   ├── Offers/
│   │   ├── Yunu.Commerce.Offers.Domain/
│   │   ├── Yunu.Commerce.Offers.Application/
│   │   ├── Yunu.Commerce.Offers.Infrastructure/
│   │   └── Yunu.Commerce.Offers.Contracts/
│   │
│   ├── Pricing/
│   │   ├── Yunu.Commerce.Pricing.Domain/
│   │   ├── Yunu.Commerce.Pricing.Application/
│   │   ├── Yunu.Commerce.Pricing.Infrastructure/
│   │   └── Yunu.Commerce.Pricing.Contracts/
│   │
│   ├── Availability/
│   │   ├── Yunu.Commerce.Availability.Domain/
│   │   ├── Yunu.Commerce.Availability.Application/
│   │   ├── Yunu.Commerce.Availability.Infrastructure/
│   │   └── Yunu.Commerce.Availability.Contracts/
│   │
│   ├── Fulfillment/
│   │   ├── Yunu.Commerce.Fulfillment.Domain/
│   │   ├── Yunu.Commerce.Fulfillment.Application/
│   │   ├── Yunu.Commerce.Fulfillment.Infrastructure/
│   │   └── Yunu.Commerce.Fulfillment.Contracts/
│   │
│   ├── Freight/
│   │   ├── Yunu.Commerce.Freight.Domain/
│   │   ├── Yunu.Commerce.Freight.Application/
│   │   ├── Yunu.Commerce.Freight.Infrastructure/
│   │   └── Yunu.Commerce.Freight.Contracts/
│   │
│   ├── Search/
│   │   ├── Yunu.Commerce.Search.Application/
│   │   ├── Yunu.Commerce.Search.Infrastructure/
│   │   └── Yunu.Commerce.Search.Contracts/
│   │
│   ├── AI/
│   │   ├── Yunu.Commerce.AI.Application/
│   │   ├── Yunu.Commerce.AI.Infrastructure/
│   │   └── Yunu.Commerce.AI.Contracts/
│   │
│   └── Integrations/
│       ├── Yunu.Commerce.Integrations.Application/
│       ├── Yunu.Commerce.Integrations.Infrastructure/
│       └── Yunu.Commerce.Integrations.Contracts/
│
└── Hosts/
    ├── Yunu.Commerce.Api/
    └── Yunu.Commerce.Worker/
```

---

# 27. Project Reference Rule

The standard reference direction inside a Domain module is:

```text
Domain
   ▲
   │
Application
   ▲
   │
Infrastructure
```

The executable host references Application and Infrastructure for composition.

Conceptually:

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
             Host
```

Infrastructure can reference Domain directly where required for repository implementations.

---

# 28. Domain Project References

A Domain project may reference only:

```text
Yunu.Commerce.SharedKernel
```

when justified.

It must not reference:

```text
Application
Infrastructure
Contracts
API
Worker
another Bounded Context Domain
```

---

# 29. Application Project References

An Application project may reference:

```text
its own Domain
approved BuildingBlocks
```

It must not reference:

```text
Infrastructure
MongoDB.Driver
Kafka clients
Redis clients
Elasticsearch SDKs
Azure SDKs
Google Cloud SDKs
```

---

# 30. Infrastructure Project References

Infrastructure may reference:

```text
its own Domain
its own Application
its own Contracts
approved BuildingBlocks
```

Infrastructure may use vendor-specific packages required by adapters.

---

# 31. Contracts Project References

Contracts projects should have minimal dependencies.

They must not depend on:

```text
Domain
Application
Infrastructure
```

Contracts must remain independently consumable.

---

# 32. Host References

Hosts may reference:

```text
Module Application projects
Module Infrastructure projects
Module Contracts where required
BuildingBlocks
```

Hosts exist as Composition Roots.

They must not contain Domain rules.

---

# 33. No Cross-Context Domain References

Forbidden examples:

```text
Pricing.Domain
    →
Catalog.Domain
```

```text
Offers.Domain
    →
Sellers.Domain
```

```text
Availability.Application
    →
Fulfillment.Infrastructure
```

Contexts communicate using:

```text
Identifiers
Contracts
Application APIs
Integration Events
Projections
```

---

# 34. Contracts Between Contexts

Example:

Search may consume:

```text
Yunu.Commerce.Catalog.Contracts
Yunu.Commerce.Pricing.Contracts
Yunu.Commerce.Availability.Contracts
```

It must not consume:

```text
Catalog.Domain
Pricing.Domain
Availability.Domain
```

---

# 35. Central Package Management

Package versions must be managed centrally through:

```text
Directory.Packages.props
```

Individual project files should normally reference packages without specifying versions.

Example:

```xml
<PackageReference Include="FluentValidation" />
```

---

# 36. Shared Build Configuration

Shared compilation settings belong in:

```text
Directory.Build.props
```

Potential settings include:

```text
Nullable enabled
ImplicitUsings enabled
Warnings configuration
Language version
Analyzer configuration
Deterministic builds
```

---

# 37. Target Framework

The target .NET framework must be defined centrally.

The exact framework version must be explicitly selected when the solution skeleton is generated.

All projects should initially use the same target framework unless a documented reason requires otherwise.

---

# 38. SDK Pinning

The repository should contain:

```text
global.json
```

to define the expected .NET SDK version used by developers, CI and build scripts.

---

# 39. Editor Configuration

The repository should contain:

```text
.editorconfig
```

to standardize:

```text
Formatting
Naming
Whitespace
C# conventions
Analyzer severity
```

---

# 40. Tests Structure

Initial test structure:

```text
tests/

├── Architecture/
│   └── Yunu.Commerce.ArchitectureTests/
│
├── Unit/
│   ├── Yunu.Commerce.Catalog.Domain.Tests/
│   ├── Yunu.Commerce.Catalog.Application.Tests/
│   ├── Yunu.Commerce.Sellers.Domain.Tests/
│   ├── Yunu.Commerce.Offers.Domain.Tests/
│   ├── Yunu.Commerce.Pricing.Domain.Tests/
│   ├── Yunu.Commerce.Availability.Domain.Tests/
│   ├── Yunu.Commerce.Fulfillment.Domain.Tests/
│   └── Yunu.Commerce.Freight.Domain.Tests/
│
└── Integration/
    └── Yunu.Commerce.IntegrationTests/
```

Not every test project needs substantial tests during the architecture skeleton phase.

---

# 41. Architecture Tests

Project:

```text
Yunu.Commerce.ArchitectureTests
```

Architecture tests must eventually validate:

```text
Domain must not reference Infrastructure

Application must not reference Infrastructure

Domain must not reference ASP.NET Core

Domain must not reference MongoDB.Driver

Application must not reference MongoDB.Driver

Application must not reference Kafka libraries

AI.Application must not reference Azure SDKs

AI.Application must not reference Google Cloud SDKs

Search.Application must not reference Elasticsearch SDKs

Contexts must not reference foreign Infrastructure projects
```

Architecture violations should fail the build pipeline.

---

# 42. Integration Tests

Integration tests will eventually validate infrastructure adapters using real dependencies.

Testcontainers should be preferred where appropriate for:

```text
MongoDB
SQL
Kafka
Redis
Elasticsearch
```

Cloud AI services should not be required for ordinary unit test execution.

---

# 43. Deployment Structure

Deployment assets live outside the .NET source tree:

```text
deploy/

├── docker/
├── kubernetes/
└── helm/
```

Deployment configuration is infrastructure, not business code.

---

# 44. Docker Development Environment

Local infrastructure may eventually contain:

```text
MongoDB
SQL
Kafka
Redis
Elasticsearch
OpenTelemetry Collector
```

The first infrastructure configuration may use Docker Compose.

---

# 45. Scripts

Repository scripts live under:

```text
scripts/
```

Examples:

```text
docs-scaffold.ps1
build.ps1
test.ps1
setup-local.ps1
docker-start.ps1
docker-stop.ps1
```

Scripts are repository tooling and must remain outside business projects.

---

# 46. Solution Folders

Inside Visual Studio, the solution should organize projects conceptually as:

```text
BuildingBlocks

Modules
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

Hosts

Tests
```

Solution Folders are organizational only.

They must not influence runtime architecture.

---

# 47. No Empty Speculative Classes

During solution skeleton generation, Copilot must not create placeholder business classes such as:

```text
Product.cs
Sku.cs
Price.cs
Seller.cs
```

unless explicitly requested.

Detailed Domain implementation will be introduced incrementally.

---

# 48. Allowed Skeleton Content

During skeleton creation, it is acceptable to create:

```text
.csproj files
solution structure
project references
dependency registration extension points
minimal assembly markers
configuration foundations
architecture tests
host startup
health-check foundation
observability foundation
```

Avoid speculative business implementation.

---

# 49. Module Registration

Each module may expose explicit registration extensions.

Example:

```text
AddCatalogApplication()
AddCatalogInfrastructure()
```

Conceptual host composition:

```csharp
builder.Services
    .AddCatalogApplication()
    .AddCatalogInfrastructure(builder.Configuration);
```

---

# 50. Module API Registration

HTTP endpoints should eventually be registered by module.

Conceptually:

```csharp
app.MapCatalogEndpoints();
app.MapPricingEndpoints();
app.MapAvailabilityEndpoints();
```

The exact endpoint style will be decided separately.

---

# 51. Worker Registration

Background processing should also be modular.

Conceptually:

```csharp
builder.Services
    .AddCatalogWorkers()
    .AddSearchWorkers()
    .AddAIWorkers();
```

The shared Worker host composes background capabilities.

---

# 52. Future Service Extraction

Suppose Catalog later requires independent deployment.

Current:

```text
Yunu.Commerce.Api
      │
      └── Catalog Module
```

Future:

```text
Yunu.Commerce.Catalog.Api
      │
      └── Catalog Module
```

Domain and Application projects should not require redesign.

Only composition and deployment topology should change.

---

# 53. Search Extraction

Search is a likely candidate for independent scaling later.

Initial:

```text
Yunu.Commerce.Worker
      │
      └── Search indexing
```

Future:

```text
Yunu.Commerce.Search.Worker
```

A dedicated:

```text
Yunu.Commerce.Search.Api
```

may also be introduced if justified.

---

# 54. AI Extraction

AI workloads may have different:

```text
scaling
cost
security
latency
provider limits
```

Therefore AI is a natural future deployment boundary.

---

# 55. Persistence Packages

Database packages belong only to Infrastructure projects.

Examples:

```text
MongoDB.Driver
    →
Catalog.Infrastructure

Entity Framework Core / Dapper
    →
appropriate Infrastructure modules

Redis client
    →
Infrastructure

Elasticsearch client
    →
Search.Infrastructure
```

They must never be globally referenced by all projects.

---

# 56. AI Packages

Provider-specific AI packages belong only in:

```text
Yunu.Commerce.AI.Infrastructure
```

No AI provider SDK should be installed in:

```text
Catalog.Domain
Catalog.Application
AI.Application
```

---

# 57. Messaging Packages

Kafka client packages belong only in messaging Infrastructure and executable hosts when technically necessary.

Domain and Application projects must remain broker-independent.

---

# 58. OpenTelemetry Packages

Observability packages should be centralized where practical through:

```text
Yunu.Commerce.Observability
```

Hosts may reference required hosting integrations.

Business Domain projects must not depend on telemetry SDKs.

---

# 59. Package Installation Principle

Do not install every anticipated package during initial scaffolding.

Only add packages required by the current architecture skeleton.

Infrastructure packages should be added when the corresponding adapter is implemented.

---

# 60. First Skeleton Goal

After Copilot creates the initial solution:

```text
dotnet restore
dotnet build
dotnet test
```

must succeed.

There must be:

```text
zero compilation errors
```

---

# 61. Initial Runtime Goal

The first skeleton may expose only basic technical endpoints such as:

```text
health
readiness
```

It does not need Product APIs yet.

The skeleton exists to prove:

```text
solution composition
dependency direction
module loading
hosting
tests
build integrity
```

---

# 62. First Business Vertical Slice

After the skeleton is approved, implementation begins with:

```text
Catalog
```

The first Domain work will define:

```text
Product
SKU
Aggregates
Entities
Value Objects
Domain Events
Repository contracts
```

Only after Domain modeling is approved will Application use cases be introduced.

---

# 63. First Infrastructure Vertical Slice

After Catalog Domain and Application are established:

```text
Catalog.Infrastructure
```

will introduce the first persistence adapter.

The initial candidate is:

```text
MongoDB
```

---

# 64. First Event Vertical Slice

Then:

```text
Create Product
      │
      ▼
Persist Product
      │
      ▼
Transactional Outbox
      │
      ▼
Kafka
```

will validate event publication.

---

# 65. First AI Vertical Slice

Then:

```text
ProductCreated
      │
      ▼
AI Enrichment
      │
      ▼
Azure OpenAI
or
Google Vertex AI
      │
      ▼
Proposed Enrichment
```

will validate AI provider abstraction.

---

# 66. First Search Vertical Slice

Then:

```text
Product Updated
      │
      ▼
Search Projection
      │
      ▼
Elasticsearch
```

will validate asynchronous projections and search infrastructure.

---

# 67. Initial Technology Boundaries

Expected technologies include:

```text
.NET
ASP.NET Core
MongoDB
Relational Database
Kafka
Redis
Elasticsearch
OpenTelemetry
Docker
Kubernetes
Azure
Generative AI provider
```

These technologies must enter only through their designated architectural boundaries.

---

# 68. Build Integrity Rule

Copilot must never leave the repository in a knowingly broken state after a completed implementation task.

After structural changes it must:

1. restore packages
2. build the entire solution
3. run architecture tests
4. run available unit tests
5. report failures
6. correct failures caused by its changes

---

# 69. No Automatic Business Generation

When generating the initial .NET solution, Copilot must not invent:

```text
Product rules
SKU rules
Pricing rules
Availability algorithms
Seller policies
Freight policies
AI prompts
```

Those will be designed separately.

The first objective is a clean architectural shell.

---

# 70. Copilot Skeleton Instructions

When instructed to generate the solution skeleton, Copilot must:

1. Read `.github/copilot-instructions.md`.

2. Read every document under:

```text
docs/architecture/
```

3. Respect the Bounded Context ownership rules.

4. Create:

```text
Yunu.Commerce.sln
src/
tests/
deploy/
```

according to this document.

5. Create all defined `.csproj` projects.

6. Configure project references according to Clean Architecture.

7. Configure Visual Studio Solution Folders.

8. Create:

```text
Directory.Build.props
Directory.Packages.props
global.json
.editorconfig
```

9. Add only baseline packages required by the skeleton.

10. Add Architecture Tests.

11. Create API and Worker hosts.

12. Add basic health-check infrastructure.

13. Add basic observability bootstrap points.

14. Build the entire solution.

15. Run tests.

16. Fix any errors introduced during scaffolding.

17. Do not implement speculative Domain behavior.

18. Provide a final summary of:

```text
projects created
references created
packages added
tests created
build result
architectural assumptions
```

---

# 71. Final Solution Shape

The expected architecture after initial scaffolding is:

```text
                         Hosts

              Yunu.Commerce.Api
              Yunu.Commerce.Worker
                       │
                       ▼
                 Application
                       │
                       ▼
                    Domain
                       ▲
                       │
                Infrastructure
                       │
       ┌───────────────┼────────────────┐
       │               │                │
       ▼               ▼                ▼
    Mongo/SQL       Kafka/Redis     Elastic/AI
```

repeated independently across the relevant Bounded Contexts.

---

# 72. Core Principle

The physical .NET solution must express the logical architecture.

Folder organization alone is not architecture.

Project references must enforce boundaries.

Tests must verify boundaries.

Documentation must explain boundaries.

The build must protect boundaries.

---

# 73. Final Rule

The initial Yunu.Commerce solution is a modular architecture prepared for distribution.

It must be:

```text
easy to understand
difficult to couple incorrectly
easy to test
easy to extend
easy to extract into services
independent from infrastructure choices
```

The skeleton establishes the boundaries.

Domain implementation will fill those boundaries incrementally.
