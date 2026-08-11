# Yunu.Commerce - Architecture Study Guide

## 1. Executive Summary

Yunu.Commerce is a modular e-commerce platform designed with:

- Domain-Driven Design (DDD)
- Clean Architecture
- Hexagonal Architecture
- Event-Driven Architecture (EDA)
- Polyglot Persistence
- Generative AI
- Cloud-native deployment

The objective is to build an architecture that can evolve without coupling the business core to databases, brokers, cloud providers or AI vendors.

The core idea is:

> Business rules remain stable. Infrastructure is replaceable.

---

# 2. Why I Am Building It This Way

The platform must support:

- Products
- SKUs
- Sellers
- 1P and 3P commerce
- Offers
- National and regional pricing
- PIX and Boleto pricing
- Availability
- Branch/store/DC availability
- Fulfillment
- Regional freight
- Search
- Generative AI

If all of this were placed inside one application model, the system would quickly become tightly coupled.

The architecture separates responsibilities so each capability can evolve independently.

---

# 3. Architecture in One Sentence

> Yunu.Commerce uses DDD to model the business, Clean Architecture to control dependency direction, Hexagonal Architecture to isolate technology, and EDA/Kafka to integrate independent contexts asynchronously.

---

# 4. DDD - Why

DDD is used because the system has complex business concepts and different areas of responsibility.

DDD helps define:

- Bounded Contexts
- Aggregates
- Entities
- Value Objects
- Domain Services
- Domain Events
- Ubiquitous Language

DDD answers:

> How should the business be modeled?

---

# 5. Clean Architecture - Why

Clean Architecture protects the business core from technical dependencies.

Dependency direction:

```text
Infrastructure
     ↓
Application
     ↓
Domain
```

The Domain does not know:

- MongoDB
- SQL
- Kafka
- Redis
- Elasticsearch
- Azure
- Google AI
- HTTP
- ASP.NET Core

Clean Architecture answers:

> Which direction may dependencies flow?

---

# 6. Hexagonal Architecture - Why

Hexagonal Architecture isolates external technologies behind Ports and Adapters.

Example:

```text
Application
    ↓
IGenerativeAiProvider
    ↓
Azure AI Adapter
or
Google AI Adapter
```

Same idea for:

- databases
- Kafka
- Redis
- Elasticsearch
- carriers
- object storage

Hexagonal Architecture answers:

> How can I replace infrastructure without changing business logic?

---

# 7. Event-Driven Architecture - Why

EDA is used because different Bounded Contexts need to react to business changes independently.

Example:

```text
PriceChanged
    ↓
Kafka
    ├─ Search updates Elasticsearch
    ├─ Cache updates Redis
    └─ Analytics can consume later
```

This avoids direct coupling.

EDA answers:

> How do independent modules communicate asynchronously?

---

# 8. Bounded Contexts

The initial business contexts are:

```text
Catalog
Sellers
Offers
Pricing
Availability
Fulfillment
Freight
```

Supporting capabilities:

```text
Search
AI
Integrations
```

---

# 9. Catalog - Why Separate

Catalog answers:

> What is the product?

Owns:

- Product
- SKU
- Brand
- Category
- Attributes
- Specifications
- Media

Catalog does not own:

- price
- seller
- stock
- freight

Reason:

Product identity and descriptive data have a different lifecycle from commercial and operational data.

---

# 10. Sellers - Why Separate

Sellers answers:

> Who is selling?

Owns:

- Seller
- SellerId
- Seller Type
- 1P / 3P
- Seller Status
- Seller lifecycle

Reason:

Seller identity must evolve independently from Product and Offer.

---

# 11. Offers - Why Separate

Offers answers:

> What Seller is offering what SKU?

Conceptually:

```text
Seller + SKU = Offer
```

Owns:

- OfferId
- SellerId
- SkuId
- Offer lifecycle

Reason:

The same SKU can be sold by multiple Sellers.

---

# 12. Pricing - Why Separate

Pricing answers:

> How much does this Offer cost?

Owns:

- National price
- Regional price
- Sale price
- PIX price
- Boleto price
- Credit-card conditions
- Validity
- Price lifecycle

Reason:

Pricing has independent rules, history, financial precision and regional/payment conditions.

---

# 13. Availability - Why Separate

Availability answers:

> Can this item currently be sold, where and in what quantity?

Owns:

- Sellable quantity
- National availability
- Regional availability
- Fulfillment-node availability
- Availability state

Reason:

Availability has a very high update frequency and different scalability characteristics from Catalog.

---

# 14. Fulfillment - Why Separate

Fulfillment answers:

> From where can the item be fulfilled?

Owns:

- Stores
- Branches
- Warehouses
- Distribution Centers
- Fulfillment Nodes
- Capabilities
- Region/service-area information

Reason:

A branch/store/DC exists independently from current stock.

---

# 15. Freight - Why Separate

Freight answers:

> How can the item be delivered, at what cost and SLA?

Owns:

- Freight Quote
- Carrier
- Delivery Method
- Freight Price
- SLA
- Delivery Promise

Reason:

Freight depends on origin, destination, logistics rules and external carriers.

---

# 16. Why Product, Offer, Price and Availability Are Not One Object

A common mistake is building:

```text
Product
├─ Seller
├─ Price
├─ Stock
├─ Freight
└─ Branches
```

This creates a giant Aggregate with unrelated lifecycles.

Instead:

```text
Catalog      → Product/SKU
Sellers      → Seller
Offers       → Seller + SKU
Pricing      → Offer price
Availability → Sellable state
Fulfillment  → Logistics nodes
Freight      → Delivery
```

This keeps boundaries clear and scalable.

---

# 17. Modular Monolith First - Why

The platform starts as a Modular Monolith rather than dozens of microservices.

Reason:

- simpler deployment
- simpler debugging
- lower operational cost
- strong module boundaries first
- easier evolution

The architecture is prepared for future service extraction.

The rule is:

> Boundary first, distribution later.

---

# 18. When I Would Extract a Microservice

A module becomes an independent service when there is a real reason:

- independent scaling
- independent deployment
- fault isolation
- different resource profile
- different team ownership
- different security requirements

Example future candidates:

- Availability
- Search
- AI workers

---

# 19. Database per Bounded Context - Why

Each context owns its canonical data.

Example:

```text
Catalog       → Catalog DB
Pricing       → Pricing DB
Availability  → Availability DB
```

This avoids hidden coupling.

Important:

Database-per-context means logical ownership.

It does not necessarily mean one physical server per context on day one.

---

# 20. Why No Cross-Context Database Joins

Forbidden:

```text
Pricing joins Catalog tables directly
```

Reason:

That bypasses Domain boundaries and makes contexts impossible to evolve independently.

Instead use:

- Integration Events
- APIs
- Projections
- Explicit contracts

---

# 21. Polyglot Persistence - Why

Different data types need different technologies.

Initial direction:

```text
Catalog       → MongoDB
Sellers       → Relational DB
Offers        → Relational DB
Pricing       → Relational DB
Availability  → MongoDB
Fulfillment   → Relational DB
Search        → Elasticsearch
Cache         → Redis
Events        → Kafka
```

Reason:

Choose storage by access pattern and consistency needs, not ideology.

---

# 22. Why MongoDB for Catalog

Catalog has:

- flexible attributes
- category-specific specifications
- nested data
- evolving schema

Document storage fits this model well.

---

# 23. Why Relational DB for Pricing

Pricing requires:

- financial precision
- consistency
- validity periods
- constraints
- history
- auditing
- concurrency

Relational storage is a strong fit.

---

# 24. Why MongoDB for Availability

Availability may have:

- high cardinality
- frequent updates
- node-specific state
- regional projections
- event-driven ingestion

MongoDB is a strong initial candidate for this workload.

---

# 25. Redis - Why

Redis is used for:

- distributed cache
- low-latency Availability projections
- temporary freight cache
- rate limiting
- selected technical state

Key rule:

> Redis makes the platform faster, not authoritative.

Canonical data stays in the owning Bounded Context.

---

# 26. Elasticsearch - Why

Elasticsearch is used for Search projections.

It enables:

- full-text search
- filtering
- facets
- sorting
- denormalized product views
- vector search
- semantic search

Key rule:

> Elasticsearch is a projection, not the source of truth.

---

# 27. Why Search Uses Projections

A customer search page may need:

```text
Product
Seller
Offer
Price
Availability
```

Instead of querying all databases synchronously, Kafka events build a denormalized Search document.

This improves:

- latency
- scalability
- resilience

---

# 28. Kafka - Why

Kafka is used as the asynchronous event backbone.

Reasons:

- high throughput
- durable streams
- replay
- consumer groups
- partition ordering
- multiple independent consumers

Example:

```text
AvailabilityChanged
        ↓
Kafka
    ├─ Redis
    └─ Elasticsearch
```

---

# 29. Why Not REST for Everything

REST is useful when an immediate answer is required.

Kafka is better when:

- many consumers need the same event
- producer should not depend on consumers
- eventual consistency is acceptable
- high throughput is required

Use both.

---

# 30. Domain Events vs Integration Events

Domain Event:

> Something happened inside the Domain.

Integration Event:

> A business fact is intentionally published outside the context.

Example:

```text
PriceChangedDomainEvent
        ↓
Application mapping
        ↓
PriceChanged
        ↓
Kafka
```

They are not the same object.

---

# 31. Transactional Outbox - Why

Problem:

```text
Save DB
then
Publish Kafka
```

If DB succeeds and Kafka fails, data and events diverge.

Solution:

```text
Same transaction:
├─ Save business state
└─ Save Outbox message
```

Then a Worker publishes the Outbox to Kafka.

Key principle:

> Commit business truth and the intent to communicate it together.

---

# 32. Why Consumers Must Be Idempotent

Kafka is treated as at-least-once delivery.

The same event may arrive more than once.

Therefore:

```text
same event processed twice
≠
business operation executed twice incorrectly
```

Use:

- EventId
- Inbox
- Source version
- Idempotency keys

---

# 33. Inbox - Why

Outbox protects the producer.

Inbox protects the consumer.

```text
Producer
  ↓
Outbox
  ↓
Kafka
  ↓
Inbox
  ↓
Consumer
```

This gives reliable asynchronous processing.

---

# 34. Elasticsearch Rebuild - Why

Search data is derived.

Therefore the system must be able to rebuild:

```text
Canonical data / Kafka
        ↓
Search Projection Worker
        ↓
New Elasticsearch index
```

Use versioned indexes and aliases.

Example:

```text
products-v1
products-v2
products-current → alias
```

---

# 35. GenAI - Why

GenAI is used to reduce manual work in Catalog operations.

Initial use case:

> Create or enrich Product/SKU information from natural language or supplier data.

AI can help with:

- title generation
- description
- category suggestion
- attributes
- specifications
- normalization
- embeddings

---

# 36. GenAI Golden Rule

> AI proposes. The Domain decides.

AI output is never authoritative by itself.

Flow:

```text
Input
  ↓
AI
  ↓
Structured Proposal
  ↓
Validation
  ↓
Catalog Domain
  ↓
Canonical Product
```

---

# 37. Why AI Does Not Write Directly to MongoDB

Because AI is probabilistic.

If AI writes directly to the database:

- Domain rules are bypassed
- invalid data can become canonical
- auditability becomes weak
- provider behavior controls business state

Therefore AI must go through Application + Domain.

---

# 38. Azure AI and Google AI - Why Both

The architecture uses provider abstraction.

```text
IGenerativeAiProvider
        ↓
   ┌────┴────┐
   ↓         ↓
 Azure     Google
```

Benefits:

- avoid vendor lock-in
- compare quality
- compare cost
- choose model by use case
- fallback if necessary
- future provider replacement

---

# 39. Structured Output - Why

For automation, AI should return structured data.

Prefer:

```json
{
  "name": "...",
  "brand": "...",
  "category": "...",
  "attributes": []
}
```

instead of a paragraph.

Benefits:

- easier validation
- safer automation
- easier testing
- lower ambiguity

---

# 40. AI Output Is Untrusted

Even valid JSON can contain invented facts.

Therefore validate:

- schema
- required fields
- allowed values
- Domain invariants
- identifiers
- evidence
- confidence/review policy

---

# 41. Embeddings - Why

Embeddings make semantic search possible.

Example:

```text
"notebook leve para viajar e programar"
```

can find relevant Products even if exact keywords differ.

Flow:

```text
Product text
    ↓
Embedding model
    ↓
Vector
    ↓
Elasticsearch
```

---

# 42. RAG - Why

RAG provides the LLM with relevant trusted context before generation.

Flow:

```text
Question
  ↓
Retrieve relevant information
  ↓
Prompt + context
  ↓
LLM
```

Use cases:

- product knowledge
- documentation
- catalog support
- future commerce assistant

RAG improves grounding but does not replace validation.

---

# 43. Tool Calling - Why

Future AI assistants can call approved Application capabilities.

Example tools:

```text
SearchCatalog
GetProduct
GetPrice
GetAvailability
GetFreight
```

The LLM does not receive direct database access.

---

# 44. Why Not Agent-First

The first implementation uses deterministic workflows plus AI calls.

Reason:

Agents add:

- autonomy
- complexity
- debugging difficulty
- cost uncertainty

Start with:

```text
Workflow
→ AI
→ Validation
→ Domain
```

Add agents only where they provide clear value.

---

# 45. AI Observability - Why

AI has variable:

- cost
- latency
- quality
- token usage

Track:

```text
Provider
Model
PromptVersion
InputTokens
OutputTokens
Latency
Cost
Status
```

This makes AI engineering measurable.

---

# 46. Prompt Versioning - Why

Changing a prompt can change behavior without changing C# code.

Therefore prompts are versioned assets.

Example:

```text
product-extraction:v1
product-extraction:v2
```

This supports regression testing and auditability.

---

# 47. Azure - Why Primary Cloud

Azure is the initial primary cloud because of:

- strong .NET ecosystem
- AKS
- Managed Identity
- Key Vault
- Entra ID
- Azure Monitor
- Application Insights
- Container Registry
- enterprise adoption

But the core remains provider-independent.

---

# 48. Azure-First, Not Azure-Locked

Key principle:

> Azure hosts Yunu.Commerce. It does not define Yunu.Commerce.

Cloud SDKs stay in Infrastructure.

The Domain remains independent.

---

# 49. AKS - Why

AKS provides:

- container orchestration
- scaling
- health probes
- rolling deployment
- workload isolation
- future service extraction

It does not mean the architecture starts with dozens of microservices.

---

# 50. Key Vault - Why

Used for secrets:

- DB credentials
- API keys
- external provider credentials
- certificates

Never commit secrets to Git.

---

# 51. Managed Identity - Why

Managed Identity avoids static credentials when accessing Azure services.

Benefits:

- fewer secrets
- automatic credential lifecycle
- better security
- RBAC integration

---

# 52. Entra ID - Why

Used for identity/authentication scenarios.

Supports:

- OAuth
- OIDC
- JWT
- enterprise identity
- service identities

Authorization still belongs to Application policies.

---

# 53. OpenTelemetry - Why

Observability should not be locked to one vendor.

OpenTelemetry provides:

- traces
- metrics
- logs

Then telemetry may flow to:

```text
Application Insights
Azure Monitor
other compatible backends
```

---

# 54. Docker - Why

Docker provides the same packaging model for:

- developer machine
- CI
- AKS

This improves environment consistency.

---

# 55. Testcontainers - Why

Integration tests can run real infrastructure:

- MongoDB
- SQL
- Kafka
- Redis
- Elasticsearch

Benefits:

- realistic tests
- repeatability
- less dependence on developer configuration

---

# 56. Architecture Tests - Why

Documentation alone does not enforce architecture.

Architecture tests can fail the build if:

```text
Domain references Infrastructure
Application references MongoDB.Driver
AI Application references Azure SDK
Domain references another context's Domain
```

This makes architecture executable.

---

# 57. First Vertical Slice

The first end-to-end business slice should be Catalog.

Flow:

```text
Natural-language Product
        ↓
GenAI
        ↓
Structured Proposal
        ↓
Catalog Application
        ↓
Catalog Domain
        ↓
MongoDB
        ↓
Outbox
        ↓
Kafka
        ↓
Search Projection
        ↓
Elasticsearch
```

This proves almost the entire architecture with one use case.

---

# 58. Why Start with Catalog

Catalog is the best first slice because it connects:

- DDD
- MongoDB
- GenAI
- Outbox
- Kafka
- Elasticsearch
- embeddings later
- API
- testing
- observability

It becomes an architectural reference implementation for the other contexts.

---

# 59. Interview Answer: Explain the Architecture

### Question

**Can you explain the architecture of your GenAI commerce project?**

### Answer

I designed it using DDD, Clean Architecture, Hexagonal Architecture and Event-Driven Architecture.

DDD separates the platform into Bounded Contexts such as Catalog, Sellers, Offers, Pricing, Availability, Fulfillment and Freight.

Clean Architecture keeps business rules independent from infrastructure.

Hexagonal Architecture isolates technologies such as MongoDB, Kafka, Redis, Elasticsearch and AI providers behind Ports and Adapters.

Kafka is used for asynchronous integration between contexts, while the Transactional Outbox guarantees reliable event publication.

Search is implemented as an Elasticsearch projection, Redis is used for low-latency cache/projections, and Generative AI is exposed through provider-neutral abstractions so I can use Azure AI or Google AI without changing the business core.

---

# 60. Interview Answer: Why DDD?

### Question

**Why did you choose DDD?**

### Answer

Because the platform has multiple complex business areas with different rules and lifecycles.

I did not want Product, Seller, Price, Availability and Freight mixed into one giant model.

DDD gives each capability clear ownership and a Ubiquitous Language.

---

# 61. Interview Answer: Why Clean Architecture?

### Question

**Why Clean Architecture?**

### Answer

To protect the Domain from technical decisions.

For example, Catalog should not care whether I persist Product in MongoDB or another database.

The same applies to Kafka, Redis, Elasticsearch and AI providers.

Infrastructure depends on the core, not the opposite.

---

# 62. Interview Answer: Why Hexagonal?

### Question

**What does Hexagonal Architecture give you here?**

### Answer

It gives me Ports and Adapters.

For example, the Application can depend on IGenerativeAiProvider while Azure AI and Google AI are separate adapters.

The same pattern applies to databases, Kafka, Search and carrier APIs.

This makes provider replacement much safer.

---

# 63. Interview Answer: Why Kafka?

### Question

**Why Kafka instead of calling services directly?**

### Answer

I still use synchronous APIs when an immediate response is required.

Kafka is used when contexts need to react independently.

For example, when a Product changes, Search and AI embedding workers can consume the same event independently.

This reduces runtime coupling and also gives me replay and horizontal consumer scaling.

---

# 64. Interview Answer: Why Outbox?

### Question

**Why use Transactional Outbox?**

### Answer

Because saving business state and publishing Kafka are two independent operations.

Without Outbox, the database can commit and Kafka can fail.

With Outbox, the Aggregate change and the Integration Event intent are committed in the same local transaction, and a Worker publishes later.

---

# 65. Interview Answer: Why Redis?

### Question

**Why Redis?**

### Answer

For low-latency distributed reads and derived projections, especially Availability.

Redis is never my default source of truth.

If Redis disappears, canonical commerce data still exists and projections can be rebuilt.

---

# 66. Interview Answer: Why Elasticsearch?

### Question

**Why Elasticsearch?**

### Answer

Because product discovery needs full-text search, filters, facets and denormalized commerce views.

It also gives me a path to vector and hybrid semantic search.

But Elasticsearch is a projection. Catalog, Pricing and Availability remain authoritative.

---

# 67. Interview Answer: Why Different Databases?

### Question

**Why not use SQL for everything?**

### Answer

Because different contexts have different data characteristics.

Catalog has flexible attributes and document-oriented structures, so MongoDB is a good fit.

Pricing benefits from relational precision and constraints.

Availability has high-frequency state updates.

I choose the persistence technology based on Domain and access patterns.

---

# 68. Interview Answer: Why Modular Monolith?

### Question

**Why not start with microservices?**

### Answer

Because distribution has a cost.

I prefer to establish strong Bounded Context and project boundaries first inside a Modular Monolith.

If Availability or Search later needs independent scaling or deployment, I can extract it without redesigning the Domain.

---

# 69. Interview Answer: How Does GenAI Fit?

### Question

**How does Generative AI fit into the architecture?**

### Answer

AI is an external reasoning capability, not the owner of business truth.

For Product registration, AI converts unstructured input into a structured proposal.

That proposal goes through Application validation and Catalog Domain rules before it becomes canonical data.

So the AI helps automate the workflow without bypassing DDD.

---

# 70. Interview Answer: Azure vs Google AI

### Question

**Why support Azure AI and Google AI?**

### Answer

I do not want the Application architecture coupled to one AI vendor.

I define provider-neutral Ports and implement Azure and Google adapters.

Then I can select based on model quality, cost, latency or capability without changing Catalog Domain.

---

# 71. Interview Answer: What Is the Source of Truth?

### Question

**What is your source-of-truth strategy?**

### Answer

Each Bounded Context owns its canonical data.

Redis and Elasticsearch are derived data.

Kafka carries Integration Events.

AI stores proposals and execution metadata but does not own Product or Price truth.

---

# 72. Interview Answer: How Do Contexts Communicate?

### Question

**How do Bounded Contexts communicate?**

### Answer

Through explicit contracts.

For synchronous needs, APIs or Application contracts.

For asynchronous propagation, versioned Integration Events through Kafka.

They never integrate by directly reading another context's database.

---

# 73. Interview Answer: Eventual Consistency

### Question

**How do you handle consistency across contexts?**

### Answer

Strong consistency stays inside the Aggregate and owning context.

Across contexts I use eventual consistency with reliable events.

For example, Pricing commits the Price, publishes PriceChanged through Outbox/Kafka, and Search updates its projection asynchronously.

---

# 74. Interview Answer: How Do You Handle Duplicates?

### Question

**Kafka can deliver messages more than once. What do you do?**

### Answer

Consumers are idempotent.

I use EventId, Inbox or source versions depending on the use case.

I design around at-least-once delivery rather than assuming perfect exactly-once processing.

---

# 75. Interview Answer: How Do You Handle Search Rebuild?

### Question

**What happens if Elasticsearch is lost?**

### Answer

Nothing canonical is lost because Elasticsearch is derived.

I can create a new versioned index, rebuild it from canonical data and/or Kafka events, validate it and then switch the alias.

---

# 76. Interview Answer: Why Architecture Tests?

### Question

**How do you prevent developers from breaking the architecture?**

### Answer

I do not rely only on documentation.

I enforce project references and architecture tests.

For example, a test can fail if Domain references MongoDB.Driver or if one Bounded Context references another context's Domain assembly.

---

# 77. Interview Answer: How Would You Scale Availability?

### Question

**Availability becomes very high volume. What would you do?**

### Answer

Availability is already isolated as its own context.

I can independently scale its consumers and read projections.

Kafka handles the event stream, MongoDB stores canonical Availability state, and Redis provides low-latency reads.

If necessary, Availability can later be extracted into its own service.

---

# 78. Interview Answer: What About Cloud Lock-In?

### Question

**Are you locked into Azure?**

### Answer

Azure is my initial infrastructure target, but not my Domain architecture.

Cloud-specific SDKs remain in Infrastructure adapters.

For example, Azure hosts the platform, while an AI workload may use Google AI.

The business core remains independent.

---

# 79. Quick Architecture Map

```text
                    CLIENTS
                       │
                       ▼
                ASP.NET Core API
                       │
                       ▼
                  APPLICATION
                       │
                       ▼
                    DOMAIN
                       │
        ┌──────────────┼───────────────┐
        ▼              ▼               ▼
   Persistence      Messaging          AI
    Adapters         Adapter         Adapter
        │              │               │
        ▼              ▼          ┌────┴────┐
   Mongo / SQL       Kafka        Azure   Google
                       │
          ┌────────────┼────────────┐
          ▼            ▼            ▼
        Redis     Elasticsearch   AI Workers
```

---

# 80. Quick Commerce Map

```text
Catalog
→ What is the product?

Sellers
→ Who is selling?

Offers
→ What Seller-SKU relationship exists?

Pricing
→ How much does it cost?

Availability
→ Can it be supplied?

Fulfillment
→ From where can it be fulfilled?

Freight
→ How can it be delivered?
```

---

# 81. Quick Technology Map

```text
C# / .NET
→ application platform

DDD
→ business modeling

Clean Architecture
→ dependency direction

Hexagonal Architecture
→ infrastructure isolation

CQRS
→ command/query separation

Kafka
→ asynchronous integration

Outbox
→ reliable event publication

Inbox
→ idempotent consumption

MongoDB
→ flexible document persistence

Relational DB
→ transactional/financial persistence

Redis
→ low-latency distributed cache/projections

Elasticsearch
→ search projections + vector search

OpenTelemetry
→ vendor-neutral observability

Docker
→ packaging

AKS
→ orchestration/scaling

Key Vault
→ secrets

Managed Identity
→ cloud authentication without static secrets

Entra ID
→ identity/OAuth/OIDC

Azure AI / Google AI
→ replaceable GenAI providers

GitHub Actions
→ CI/CD

Testcontainers
→ realistic integration tests
```

---

# 82. The Most Important Interview Message

The strongest architectural message is:

> I am not using technologies because they are fashionable. Each technology has a specific responsibility and boundary.

Examples:

```text
MongoDB
because Catalog data is flexible.

SQL
because Pricing needs financial consistency.

Kafka
because contexts need decoupled asynchronous integration.

Redis
because Availability needs very low read latency.

Elasticsearch
because customer discovery needs a denormalized Search model.

GenAI
because it can automate catalog onboarding, but it remains outside the Domain.

Azure
because it is the first cloud platform, while business logic remains cloud-independent.
```

---

# 83. Final Summary

Yunu.Commerce is designed so the platform can grow in complexity without turning into one tightly coupled system.

The architecture intentionally separates:

```text
business logic
application orchestration
infrastructure
integration
search
AI
cloud deployment
```

The final engineering principle is:

> Domains own business truth. Application orchestrates. Infrastructure adapts. Events integrate. Search projects. AI assists. Cloud hosts.
