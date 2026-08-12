# Yunu.Commerce - Copilot Solution Bootstrap Instructions

## Purpose

This document contains the bootstrap instructions to send to GitHub Copilot when generating the initial Yunu.Commerce .NET solution skeleton.

The repository documentation is the architectural source of truth.

Do not implement business logic during this step.

---

# Copilot Prompt

Proceed with option 1.

Use all existing architecture documentation and `.github/copilot-instructions.md` as the source of truth.

Before proceeding, explicitly read these architecture files:

- `.github/copilot-instructions.md`
- `docs/architecture/01-system-overview.md`
- `docs/architecture/02-bounded-contexts.md`
- `docs/architecture/03-clean-architecture.md`
- `docs/architecture/04-hexagonal-architecture.md`
- `docs/architecture/05-event-driven-architecture.md`
- `docs/architecture/06-solution-structure.md`
- `docs/adr/0001-use-ddd-clean-hexagonal.md`
- `docs/adr/0002-bounded-context-strategy.md`
- `docs/adr/0003-database-per-bounded-context.md`
- `docs/adr/0004-use-kafka-for-event-driven-integration.md`
- `docs/adr/0005-use-transactional-outbox.md`
- `docs/adr/0006-use-redis-for-distributed-cache.md`
- `docs/adr/0007-use-elasticsearch-for-search-projections.md`
- `docs/adr/0008-genai-provider-abstraction.md`
- `docs/adr/0009-cloud-provider-strategy.md`

Use the Bounded Contexts already defined in the documentation:

- Catalog
- Sellers
- Offers
- Pricing
- Availability
- Fulfillment
- Freight

Also include the supporting capabilities already defined by the architecture:

- Search
- AI
- Integration
- BuildingBlocks
- Hosts
- Tests

Where `06-solution-structure.md` does not explicitly enumerate a detail, use the smallest reasonable assumption consistent with the existing architecture and clearly document that assumption.

Do NOT implement business logic.

I want you to generate a single idempotent PowerShell bootstrap script:

`tools/bootstrap-solution.ps1`

The script must create the complete initial Yunu.Commerce .NET solution skeleton, including:

- `Yunu.Commerce.sln`
- `src/` structure
- `tests/` structure
- all required `.csproj` projects
- Visual Studio Solution Folders
- project references
- `Directory.Build.props`
- `Directory.Packages.props`
- `global.json`
- `.editorconfig`
- minimal API Host
- minimal Worker Host
- Architecture Tests project
- dependency injection extension points
- minimal health-check bootstrap
- minimal OpenTelemetry bootstrap where appropriate

Follow the Clean Architecture dependency rule strictly:

```text
Domain
↑
Application
↑
Infrastructure
↑
Host
```

Important rules:

- Domain must never reference Application or Infrastructure.
- Application may reference its own Domain.
- Infrastructure may reference its own Application and Domain as required.
- Hosts may compose Application and Infrastructure.
- A Bounded Context Domain must never reference another Bounded Context Domain.
- Do not create shared business entities.
- Do not implement Product, SKU, Seller, Offer, Price, Availability, Fulfillment or Freight business logic yet.
- Do not create speculative Aggregates, Entities, Value Objects or Domain Services.
- Do not add MongoDB, Kafka, Redis, Elasticsearch, Azure AI or Google AI implementation code yet.
- Keep provider-specific dependencies outside Domain and Application.
- Before generating or modifying the solution structure, read and comply with the following ADRs individually:
  - `docs/adr/0001-use-ddd-clean-hexagonal.md`
  - `docs/adr/0002-bounded-context-strategy.md`
  - `docs/adr/0003-database-per-bounded-context.md`
  - `docs/adr/0004-use-kafka-for-event-driven-integration.md`
  - `docs/adr/0005-use-transactional-outbox.md`
  - `docs/adr/0006-use-redis-for-distributed-cache.md`
  - `docs/adr/0007-use-elasticsearch-for-search-projections.md`
  - `docs/adr/0008-genai-provider-abstraction.md`
  - `docs/adr/0009-cloud-provider-strategy.md`
- Treat these ADR files as mandatory architectural references.
- If an implementation decision conflicts with an ADR, report the conflict or assumption before proceeding.

Additional guardrails:

- `Yunu.Commerce.SharedKernel` must remain intentionally minimal.
- SharedKernel may contain only truly shared primitives or abstractions.
- Do not place Product, Seller, Offer, Price, Availability, Fulfillment, Freight or any Bounded Context business model in SharedKernel.
- `Yunu.Commerce.EventBus` must contain messaging abstractions/building blocks only.
- Do not place Kafka-specific implementation in Domain or Application projects.
- Contracts projects must contain contracts/DTOs only and no Domain behavior.
- Search, AI and Integrations remain supporting capabilities without Domain projects.
- Do not create cross-context database access.
- Do not create cross-context Domain references.
- Add Architecture Tests that verify dependency rules.

The PowerShell script must be safe to run more than once whenever practical.

At the end of the script execute:

```text
dotnet restore
dotnet build
dotnet test
```

Stop immediately if one of these commands fails and return a useful error.

Also generate:

`docs/copilot/bootstrap-solution-assumptions.md`

containing every architectural assumption you needed to make because it was not explicitly defined in the documentation.

Before generating the script, show me the proposed project tree and project-reference graph for approval.

Do not proceed beyond the architecture skeleton.

---

# Expected Project Direction

The proposed structure should remain aligned with the documented architecture.

A representative high-level structure is:

```text
Yunu.Commerce.sln

src/
├── BuildingBlocks/
│   ├── Yunu.Commerce.SharedKernel/
│   ├── Yunu.Commerce.Contracts/
│   ├── Yunu.Commerce.EventBus/
│   ├── Yunu.Commerce.Observability/
│   └── Yunu.Commerce.Security/
│
├── Modules/
│   ├── Catalog/
│   │   ├── Yunu.Commerce.Catalog.Domain/
│   │   ├── Yunu.Commerce.Catalog.Application/
│   │   ├── Yunu.Commerce.Catalog.Infrastructure/
│   │   └── Yunu.Commerce.Catalog.Contracts/
│   │
│   ├── Sellers/
│   ├── Offers/
│   ├── Pricing/
│   ├── Availability/
│   ├── Fulfillment/
│   ├── Freight/
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

tests/
├── Architecture/
│   └── Yunu.Commerce.ArchitectureTests/
│
├── Unit/
│   ├── Catalog/
│   ├── Sellers/
│   ├── Offers/
│   ├── Pricing/
│   ├── Availability/
│   ├── Fulfillment/
│   └── Freight/
│
└── Integration/
    └── Yunu.Commerce.IntegrationTests/
```

The exact structure must follow:

`docs/architecture/06-solution-structure.md`

---

# Dependency Rules

For every business module:

```text
Domain
  ↑
Application
  ↑
Infrastructure
  ↑
Host
```

Examples:

```text
Yunu.Commerce.Catalog.Domain
        ↑
Yunu.Commerce.Catalog.Application
        ↑
Yunu.Commerce.Catalog.Infrastructure
        ↑
Yunu.Commerce.Api
```

Supporting capabilities follow:

```text
Search.Application
        ↑
Search.Infrastructure

AI.Application
        ↑
AI.Infrastructure

Integrations.Application
        ↑
Integrations.Infrastructure
```

No supporting capability may introduce direct dependencies that violate the existing ADRs.

---

# Forbidden Examples

Do not generate dependencies such as:

```text
Catalog.Domain
    ↓
MongoDB.Driver
```

Do not generate:

```text
Pricing.Domain
    ↓
Catalog.Domain
```

Do not generate:

```text
AI.Application
    ↓
Azure AI SDK
```

Do not generate:

```text
Availability.Domain
    ↓
StackExchange.Redis
```

Do not generate:

```text
Search.Application
    ↓
Elasticsearch concrete client
```

Do not generate shared business models inside:

```text
Yunu.Commerce.SharedKernel
```

---

# Completion Gate

The architecture bootstrap task is complete only when:

```text
dotnet restore
dotnet build
dotnet test
```

complete successfully.

After that, stop.

Do not implement Product, SKU or any other business Domain yet.

The next phase will be explicitly requested in a separate Copilot task.
