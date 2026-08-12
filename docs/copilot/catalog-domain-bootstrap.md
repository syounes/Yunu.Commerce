# Yunu.Commerce - Catalog Domain Bootstrap Instructions

## Purpose

This document defines the GitHub Copilot task for implementing the **first real business Domain slice** of Yunu.Commerce:

```text
Catalog.Domain
```

This phase begins only after the Yunu.Commerce solution skeleton has been generated successfully and:

```text
dotnet restore
dotnet build
dotnet test
```

all complete successfully.

The objective of this task is to implement the initial Catalog Domain model and its Domain tests while preserving every architectural boundary already defined for Yunu.Commerce.

This task is **Domain only**.

Do not implement Application, Infrastructure, MongoDB, Kafka, Elasticsearch, Redis or Generative AI integration during this phase.

---

# Mandatory Sources of Truth

Before generating or modifying Catalog Domain code, explicitly read:

- `.github/copilot-instructions.md`

Architecture:

- `docs/architecture/01-system-overview.md`
- `docs/architecture/02-bounded-contexts.md`
- `docs/architecture/03-clean-architecture.md`
- `docs/architecture/04-hexagonal-architecture.md`
- `docs/architecture/05-event-driven-architecture.md`
- `docs/architecture/06-solution-structure.md`

Catalog Domain:

- `docs/domains/catalog.md`

Data Architecture:

- `docs/data/data-architecture.md`

AI Architecture:

- `docs/ai/ai-architecture.md`

Integration Architecture:

- `docs/integration/integration-architecture.md`

ADRs:

- `docs/adr/0001-use-ddd-clean-hexagonal.md`
- `docs/adr/0002-bounded-context-strategy.md`
- `docs/adr/0003-database-per-bounded-context.md`
- `docs/adr/0004-use-kafka-for-event-driven-integration.md`
- `docs/adr/0005-use-transactional-outbox.md`
- `docs/adr/0006-use-redis-for-distributed-cache.md`
- `docs/adr/0007-use-elasticsearch-for-search-projections.md`
- `docs/adr/0008-genai-provider-abstraction.md`
- `docs/adr/0009-cloud-provider-strategy.md`

Treat these documents as mandatory architectural references.

Do not treat a directory such as `docs/adr/` as a file target.

If the documentation is ambiguous or two documents appear to conflict:

1. do not silently invent a rule;
2. identify the ambiguity;
3. choose the smallest assumption only when necessary;
4. document the assumption before implementation.

---

# Current Phase

Implement only:

```text
src/Modules/Catalog/Yunu.Commerce.Catalog.Domain
```

and its corresponding Domain tests:

```text
tests/Unit/Yunu.Commerce.Catalog.Domain.Tests
```

Do not modify other Bounded Contexts unless a compilation issue caused by this task makes a minimal change strictly necessary.

Do not proceed to:

```text
Catalog.Application
Catalog.Infrastructure
Catalog.Contracts
MongoDB
Outbox
Kafka
Elasticsearch
Redis
Azure AI
Google AI
API endpoints
```

Those are separate future tasks.

---

# Catalog Responsibility

Catalog answers:

> What is the Product and what are its SKUs?

Catalog owns the canonical descriptive identity of:

```text
Product
SKU
Brand references
Category references
Attributes
Specifications
Media metadata
Product lifecycle
SKU lifecycle
```

Catalog does not own:

```text
Seller
Offer
Price
Availability
Fulfillment
Freight
Payment pricing
Search projection
AI execution state
```

Do not introduce these foreign business responsibilities into Catalog.Domain.

---

# Domain Architecture Rules

The dependency direction remains:

```text
Domain
↑
Application
↑
Infrastructure
↑
Hosts
```

For this task:

```text
Yunu.Commerce.Catalog.Domain
```

may depend only on:

- .NET base libraries
- the intentionally minimal `Yunu.Commerce.SharedKernel`, when genuinely necessary

Catalog.Domain must not reference:

```text
Yunu.Commerce.Catalog.Application
Yunu.Commerce.Catalog.Infrastructure
Yunu.Commerce.Catalog.Contracts

Yunu.Commerce.Sellers.Domain
Yunu.Commerce.Offers.Domain
Yunu.Commerce.Pricing.Domain
Yunu.Commerce.Availability.Domain
Yunu.Commerce.Fulfillment.Domain
Yunu.Commerce.Freight.Domain

MongoDB.Driver
Entity Framework Core
Dapper
Confluent.Kafka
StackExchange.Redis
Elasticsearch client libraries
Azure SDKs
Google SDKs
ASP.NET Core
HTTP clients
OpenTelemetry
```

---

# Modeling Style

Use tactical DDD only where the Catalog documentation justifies it.

Prefer explicit Domain concepts over anemic public setters.

Potential concepts documented for Catalog include:

```text
Product
SKU
ProductId
SkuId
ProductName
SkuCode
BrandId
CategoryId
ProductStatus
SkuStatus
ProductAttribute
ProductSpecification
Media
```

Do not create a concept merely because it appears fashionable in DDD.

Every Aggregate, Entity, Value Object, Domain Service and Domain Event must have an explicit reason grounded in `docs/domains/catalog.md`.

---

# Aggregate Boundary

The initial Aggregate Root candidate is:

```text
Product
```

Before implementation, verify this against `docs/domains/catalog.md`.

The Product Aggregate should protect the consistency rules that genuinely belong to Product and its owned state.

Do not create an oversized Aggregate.

Do not include:

```text
Offers
Prices
Availability
Sellers
FulfillmentNodes
FreightQuotes
```

inside Product.

---

# SKU Modeling

SKU belongs to Catalog but its exact tactical relationship with Product must follow the Catalog documentation.

If the documentation establishes SKU as part of the Product Aggregate, implement it accordingly.

Do not independently promote SKU to a separate Aggregate Root unless the existing documentation clearly requires that decision.

If this point is ambiguous, stop and report the ambiguity before changing the documented Aggregate boundary.

---

# Strongly Typed IDs

Use strongly typed Domain identifiers where consistent with the existing architecture.

Examples:

```text
ProductId
SkuId
BrandId
CategoryId
```

Identifiers must remain database-independent.

Do not use:

```text
Mongo ObjectId
SQL Identity
Elasticsearch document identifiers
```

as Domain identity types.

---

# Value Objects

Create Value Objects only for concepts with meaningful validation or semantic behavior.

Potential examples may include:

```text
ProductName
SkuCode
```

depending on rules explicitly supported by Catalog documentation.

Do not create dozens of wrapper types with no behavioral or semantic benefit.

Value Objects should:

- be immutable;
- validate their own invariants where appropriate;
- compare by value;
- avoid Infrastructure concerns.

---

# Entities

Entities must have identity and lifecycle semantics.

Avoid classes with unrestricted setters.

Prefer behavior such as:

```text
Rename(...)
Activate(...)
Deactivate(...)
AddSku(...)
UpdateSpecification(...)
```

only when those operations are actually supported by the documented Catalog rules.

Do not invent business behavior.

---

# Encapsulation

Aggregate state should not be freely mutable from outside.

Avoid:

```csharp
public string Name { get; set; }
public List<Sku> Skus { get; set; }
```

Prefer controlled state changes.

Collections should normally be exposed as read-only views when external mutation would bypass invariants.

---

# Validation

Separate:

```text
Domain invariants
```

from:

```text
Application input validation
```

Domain must protect rules that must always be true regardless of caller.

Do not add FluentValidation or another Application validation framework to Catalog.Domain.

---

# Domain Events

Create Domain Events only for meaningful Catalog business facts supported by the documentation.

Potential examples may include:

```text
ProductCreatedDomainEvent
ProductUpdatedDomainEvent
SkuCreatedDomainEvent
SkuUpdatedDomainEvent
```

Do not create all possible events automatically.

Not every setter or field change deserves a Domain Event.

Domain Events must remain provider-neutral.

They must not know Kafka.

---

# Integration Events

Do not implement Integration Events in this task.

The future flow is:

```text
Domain Event
      ↓
Application mapping
      ↓
Integration Event
      ↓
Outbox
      ↓
Kafka
```

Catalog.Domain owns Domain Events only.

Kafka contracts belong outside the Domain.

---

# Persistence Independence

Catalog is expected to use MongoDB later, but Catalog.Domain must not know this.

Do not add:

```text
BsonId
BsonElement
BsonRepresentation
MongoDB.Driver
MongoDB attributes
Mongo-specific base classes
```

to Domain types.

The Product model must remain valid if persistence changes.

---

# AI Independence

Generative AI will later help create Product proposals.

Do not add AI concerns to Product or SKU.

Forbidden examples:

```text
AzureAiProduct
GeminiProduct
Prompt
ModelName
TokenUsage
AIConfidence
AIResponse
```

inside Catalog.Domain unless a future Domain decision explicitly makes such a concept business-relevant.

The future AI flow is:

```text
AI
↓
Structured Proposal
↓
Catalog.Application
↓
Catalog.Domain
```

AI output is input to the Domain, not part of the Domain implementation itself.

---

# Search Independence

Do not add Elasticsearch concepts to Catalog.Domain.

Catalog owns canonical Product data.

Search owns a future derived projection.

Future flow:

```text
Catalog
↓
ProductChanged
↓
Kafka
↓
Search Projection
↓
Elasticsearch
```

---

# SharedKernel Rule

`Yunu.Commerce.SharedKernel` must remain intentionally minimal.

Do not move Catalog-specific concepts into SharedKernel.

Forbidden SharedKernel concepts include:

```text
Product
SKU
ProductName
Category
Brand
ProductStatus
```

unless a future ADR explicitly establishes otherwise.

A concept belongs in SharedKernel only when its semantics are genuinely identical across Bounded Contexts.

---

# Suggested Initial Namespace Structure

Use a simple structure aligned to business concepts.

A possible direction is:

```text
Yunu.Commerce.Catalog.Domain/
├── Products/
│   ├── Product.cs
│   ├── ProductId.cs
│   ├── ProductName.cs
│   ├── ProductStatus.cs
│   │
│   ├── Skus/
│   │   ├── Sku.cs
│   │   ├── SkuId.cs
│   │   ├── SkuCode.cs
│   │   └── SkuStatus.cs
│   │
│   └── Events/
│       └── ...
│
├── Brands/
│   └── BrandId.cs
│
└── Categories/
    └── CategoryId.cs
```

This is a suggested organizational shape only.

Before creating it, verify the actual concepts against `docs/domains/catalog.md`.

Prefer fewer meaningful folders over excessive ceremony.

---

# Do Not Create Yet

Do not create:

```text
IProductRepository implementation
MongoProductRepository
Mongo mappings
DbContext
Mongo collections
Kafka producer
Outbox persistence
Inbox
Redis cache
Elasticsearch document
Search index
AI provider
AI prompts
HTTP endpoint
Controller
Minimal API endpoint
CreateProductCommand
CreateProductHandler
GetProductQuery
```

Those belong to future phases.

A repository Port should also not be introduced automatically unless the existing architecture explicitly places that abstraction inside Domain and we actually need it for the next phase.

---

# Unit Tests

Implement meaningful Domain unit tests in:

```text
tests/Unit/Yunu.Commerce.Catalog.Domain.Tests
```

Tests should verify documented Domain behavior.

Potential categories include:

```text
valid Product creation
invalid Product creation
Product identity
Product name invariants
SKU creation
duplicate SKU protection if documented
Product lifecycle transitions if documented
SKU lifecycle transitions if documented
collection encapsulation
Domain Event creation where implemented
Value Object equality
```

Do not create meaningless tests solely to inflate test count.

Remove the bootstrap `PlaceholderTests` once real tests replace them.

---

# Test Style

Tests should be:

- deterministic;
- isolated;
- readable;
- independent from databases;
- independent from network;
- independent from Azure/Google;
- independent from Kafka/Redis/Elasticsearch.

Use xUnit as already established by the solution bootstrap.

Follow the existing project conventions.

---

# Architecture Tests

Do not weaken existing Architecture Tests.

After implementing Catalog.Domain, all existing architecture rules must still pass.

If an architecture test fails, fix the implementation rather than bypassing the test.

Do not add exclusions simply to make a violation pass.

---

# Error Modeling

Use the Domain error/exception strategy already established in the architecture or existing SharedKernel.

Do not introduce a second competing error framework without need.

If no explicit error strategy exists for a specific invariant, choose the smallest implementation consistent with existing code and document the assumption.

---

# Time

Use UTC-oriented types for Domain timestamps when timestamps are part of documented business state.

Prefer:

```text
DateTimeOffset
```

when an absolute timestamp is required.

Do not call `DateTime.Now` inside Domain behavior.

If current time is needed for a documented rule, prefer an explicit value supplied to the Domain or an approved clock abstraction.

Do not invent temporal rules during this phase.

---

# Immutability and Constructors

Do not create public parameterless constructors only to satisfy a future ORM/ODM.

Persistence mapping concerns belong to Infrastructure.

Constructors/factory methods should serve Domain correctness.

---

# Coding Rules

Use:

- nullable reference types;
- clear business names;
- explicit access modifiers;
- private setters/fields where appropriate;
- read-only collection exposure;
- small methods;
- meaningful tests.

Avoid:

- generic repositories without need;
- base classes with speculative abstraction;
- reflection tricks;
- infrastructure attributes;
- unnecessary inheritance;
- public mutation;
- primitive obsession where a real Domain concept clearly exists;
- wrapper explosion where no semantic value exists.

---

# Implementation Process

Before creating code, produce a short implementation proposal containing:

1. Aggregate Root(s) to be created.
2. Entities to be created.
3. Value Objects to be created.
4. Enums/status concepts to be created.
5. Domain Events to be created.
6. Invariants supported directly by `docs/domains/catalog.md`.
7. Unit test scenarios.
8. Any ambiguity or assumption.

Do not modify files until the proposal has been shown for review.

---

# After Approval

After the implementation proposal is approved:

1. implement only Catalog.Domain;
2. implement Catalog.Domain unit tests;
3. remove obsolete Catalog Domain placeholder tests;
4. do not implement Application or Infrastructure;
5. run:

```text
dotnet restore
dotnet build
dotnet test
```

6. fix only problems introduced by this task;
7. keep all Architecture Tests passing.

---

# Completion Gate

This phase is complete only when:

```text
dotnet restore
dotnet build
dotnet test
```

all succeed and Catalog.Domain contains meaningful business behavior covered by unit tests.

The Copilot must then stop.

Do not proceed automatically to:

```text
Catalog.Application
Catalog.Infrastructure
MongoDB
Outbox
Kafka
Search
GenAI
```

The next phase will be explicitly requested.

---

# Final Rule

The objective is not to produce the maximum amount of DDD code.

The objective is:

> Build the smallest correct Catalog Domain model that protects the documented business invariants and remains completely independent from infrastructure.

Prefer a compact, explicit and testable Domain over speculative architecture.
