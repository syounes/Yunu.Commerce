# ADR-0002: Bounded Context Strategy

- **Status:** Accepted
- **Date:** 2026-08-11
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Domain decomposition and context boundaries

## 1. Context

Yunu.Commerce covers several commerce capabilities with different business rules, data ownership, scalability requirements and integration patterns.

Treating the entire platform as one large Domain model would create excessive coupling between concepts such as Product, Seller, Offer, Price, Availability, Fulfillment and Freight.

The architecture therefore requires explicit Bounded Contexts with independent models and ownership.

## 2. Decision

Yunu.Commerce will initially use the following core Bounded Contexts:

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

Search and AI are supporting platform/application capabilities rather than owners of the canonical commerce entities.

## 3. Context Responsibilities

### Catalog

Owns the canonical definition of what a product is.

Primary concepts:

```text
Product
SKU
Category
Brand
Attributes
Specifications
Media
Product lifecycle
```

Catalog does not own commercial price, inventory quantity or freight quotation.

### Sellers

Owns seller identity and lifecycle.

Primary concepts:

```text
Seller
Seller Type
1P / 3P classification
Seller Status
External References
Commercial identity
```

### Offers

Owns the commercial relationship between a Seller and a SKU.

Conceptually:

```text
Seller
  +
SKU
  =
Offer
```

Primary concepts:

```text
Offer
SellerId
SkuId
Offer Status
External Offer Reference
Commercial lifecycle
```

Catalog does not own Offers.

### Pricing

Owns sell prices and payment-specific commercial pricing.

Primary concepts:

```text
Price
Regular Price
Sale Price
PIX Price
Boleto Price
Regional Price
National Price
Validity
Currency
```

Pricing references commercial identities but owns price rules.

### Availability

Owns sellable availability.

Primary concepts:

```text
Availability
On-hand quantity
Reserved quantity
Sellable quantity
National availability
Regional availability
Availability status
```

Availability does not own Product definition or Fulfillment Node identity.

### Fulfillment

Owns the physical/logistical network from which commerce can be fulfilled.

Primary concepts:

```text
FulfillmentNode
Branch
Distribution Center
Store
Region
Capabilities
Pickup capability
Delivery capability
```

### Freight

Owns freight quotation and freight-specific policies.

Primary concepts:

```text
Freight Quote
Delivery Option
Carrier
Freight Cost
Estimated Delivery
Regional freight rules
```

Freight consumes information from other capabilities but does not become their owner.

## 4. Why Separate Catalog and Offers

A Product/SKU describes what is sold.

An Offer describes who sells a SKU commercially.

Example:

```text
SKU: iPhone 17 Pro 256 GB
        │
        ├── Seller A → Offer A
        ├── Seller B → Offer B
        └── Seller C → Offer C
```

Putting Seller and Offer lifecycle inside Product would create a large and unstable Catalog Aggregate.

Therefore:

```text
Catalog owns SKU

Offers references SkuId
```

## 5. Why Separate Offers and Pricing

An Offer identifies a commercial offering.

Pricing defines how much that offering costs under a scope and payment condition.

This allows Pricing to evolve independently with:

```text
national prices
regional prices
PIX prices
Boleto prices
campaign prices
future installment policies
```

without bloating the Offer model.

## 6. Why Separate Availability

Availability has very different operational characteristics from Catalog.

Catalog tends to have:

```text
lower write frequency
rich descriptive data
product lifecycle
```

Availability tends to have:

```text
high-frequency writes
high-frequency reads
regional/node granularity
event-driven updates
low-latency requirements
```

These characteristics justify independent ownership.

## 7. Why Separate Fulfillment and Availability

Fulfillment answers:

```text
What logistical nodes exist?
What can each node do?
Where is each node located?
```

Availability answers:

```text
How much of a commercial item is currently sellable at a node/scope?
```

A branch can exist even when it has zero inventory.

Therefore the concepts have different lifecycles.

## 8. Why Separate Freight

Freight quotation combines information from several sources:

```text
destination
fulfillment origin
product dimensions/weight
carrier
service level
commercial policies
```

Freight is a distinct capability because quotation behavior and carrier integrations evolve independently.

## 9. Context Map

Initial conceptual context map:

```text
                    ┌─────────────┐
                    │   Catalog   │
                    └──────┬──────┘
                           │ Product/SKU facts
                           ▼
                    ┌─────────────┐
                    │   Offers    │◄────────────┐
                    └──────┬──────┘             │
                           │                    │
                ┌──────────┴──────────┐         │
                ▼                     ▼         │
          ┌───────────┐        ┌──────────────┐ │
          │  Pricing  │        │ Availability │ │
          └───────────┘        └──────┬───────┘ │
                                      │         │
                                      ▼         │
                               ┌─────────────┐  │
                               │ Fulfillment │  │
                               └──────┬──────┘  │
                                      │         │
                                      ▼         │
                                ┌───────────┐   │
                                │  Freight  │   │
                                └───────────┘   │
                                               │
                    ┌─────────────┐            │
                    │   Sellers   │────────────┘
                    └─────────────┘
```

This diagram shows relationships, not direct project dependencies.

## 10. Context Communication

Bounded Contexts communicate through explicit contracts.

Allowed mechanisms:

```text
Integration Events
REST/Application APIs
Read Projections
Explicit IDs
Process Managers when justified
```

Forbidden mechanism:

```text
Direct access to another context's database
```

## 11. Cross-Context Identity

Contexts may reference identities owned elsewhere.

Examples:

```text
Offers
    SellerId
    SkuId

Pricing
    OfferId

Availability
    SkuId or OfferId
    FulfillmentNodeId

Freight
    FulfillmentNodeId
```

A reference does not transfer ownership.

## 12. No Cross-Context Aggregate References

Forbidden:

```csharp
public class Offer
{
    public Product Product { get; }
    public Seller Seller { get; }
}
```

Preferred conceptually:

```csharp
public class Offer
{
    public SkuId SkuId { get; }
    public SellerId SellerId { get; }
}
```

The exact C# implementation will be defined during Domain implementation.

## 13. Independent Ubiquitous Language

Each context may use terminology that is precise within its own model.

The same real-world concept may have different representations in different contexts.

This is acceptable.

Do not force one universal enterprise object model across every context.

## 14. Canonical Commerce Model

"Canonical" in Yunu.Commerce means stable internal contracts and language at integration boundaries.

It does not mean one giant shared Domain model.

Each Bounded Context remains autonomous.

## 15. Shared Kernel

A Shared Kernel must be kept intentionally small.

Potential technical/value concepts include:

```text
Result
Error
Strongly typed identifiers infrastructure
Money primitives where semantically identical
Domain Event base abstractions
Clock abstraction
```

Business entities must not be placed in a Shared Kernel merely to reuse code.

## 16. Shared Kernel Warning

Forbidden:

```text
Shared/
├── Product.cs
├── Seller.cs
├── Price.cs
└── Availability.cs
```

This would recreate a distributed monolith inside a shared library.

## 17. Search Capability

Search consumes data from multiple contexts and builds denormalized projections.

Conceptually:

```text
Catalog ────────┐
Sellers ────────┤
Offers ─────────┤
Pricing ────────┤
Availability ───┤
Fulfillment ────┤
                ▼
              Search
                │
                ▼
         Elasticsearch
```

Search does not own the underlying commerce facts.

## 18. AI Capability

AI assists business workflows but does not own Product, Price, Availability or other commerce truth.

Conceptually:

```text
AI
 │
 ▼
Proposal
 │
 ▼
Owning Application
 │
 ▼
Owning Domain
```

For Product registration:

```text
AI → Catalog Application → Catalog Domain
```

## 19. Context Data Ownership

Each context owns its canonical persistence.

Conceptually:

```text
Catalog       → Catalog data
Sellers       → Seller data
Offers        → Offer data
Pricing       → Pricing data
Availability  → Availability data
Fulfillment   → Fulfillment data
Freight       → Freight-specific data
```

Physical database technology is decided separately.

See ADR-0003.

## 20. Event Ownership

The context where a business fact occurs owns publication semantics.

Examples:

```text
Catalog
→ ProductCreated
→ ProductUpdated

Sellers
→ SellerActivated

Offers
→ OfferActivated

Pricing
→ PriceChanged

Availability
→ AvailabilityChanged

Fulfillment
→ FulfillmentNodeChanged
```

Consumers do not dictate producer internal Domain Events.

## 21. Integration Contracts

Integration contracts must expose only what consumers legitimately require.

Do not serialize entire Aggregates into events by default.

Prefer focused business contracts.

## 22. Consistency

Strong consistency is primarily maintained within Aggregate and Bounded Context boundaries.

Across contexts:

```text
eventual consistency
```

is the default.

Example:

```text
Price changed
    │
    ▼
Pricing committed
    │
    ▼
PriceChanged
    │
    ▼
Search eventually updated
```

## 23. Cross-Context Transactions

Distributed transactions across contexts are not part of the default architecture.

Forbidden design goal:

```text
One ACID transaction across
Catalog + Pricing + Availability + Search
```

Use local transactions plus reliable events.

## 24. Application Composition

Some customer-facing use cases require data from several contexts.

Example:

```text
Product Detail
=
Catalog
+ Offer
+ Seller
+ Price
+ Availability
```

This does not justify merging the Domains.

Use:

```text
Read Projection
API Composition
Application orchestration
```

depending on freshness and latency requirements.

## 25. Modular Monolith Deployment

Initially, several contexts may execute inside the same process.

Example:

```text
Yunu.Commerce.Api

├── Catalog Module
├── Sellers Module
├── Offers Module
├── Pricing Module
├── Availability Module
├── Fulfillment Module
└── Freight Module
```

Logical boundaries remain mandatory even when deployment is shared.

## 26. Boundary Before Distribution

The rule is:

> First create strong module boundaries. Distribute them only when operational reasons justify it.

Microservices are a deployment decision, not the definition of DDD.

## 27. Future Extraction

Potential extraction candidates may include:

```text
Availability
Search
AI processing
Pricing
```

if they develop significantly different:

```text
scale
availability requirements
deployment cadence
team ownership
resource profiles
```

Extraction should preserve existing contracts.

## 28. Context Internal Architecture

Each business context follows the architecture defined by ADR-0001.

Conceptually:

```text
Context
│
├── Domain
├── Application
├── Infrastructure
└── Contracts / Host as required
```

Example:

```text
Catalog
│
├── Yunu.Commerce.Catalog.Domain
├── Yunu.Commerce.Catalog.Application
├── Yunu.Commerce.Catalog.Infrastructure
└── Yunu.Commerce.Catalog.Contracts
```

## 29. Domain Project References

A Domain project may reference:

```text
.NET base libraries
approved minimal Building Blocks
```

It must not reference another context's Domain project.

## 30. Application Project References

Application may reference:

```text
its own Domain
approved Building Blocks
its own Contracts where appropriate
```

It must not import another context's Domain to coordinate business behavior.

## 31. Infrastructure Project References

Infrastructure may implement its context's ports and integrate through public contracts.

Provider SDKs remain isolated here.

## 32. Anti-Corruption Layers

External systems and legacy models must be translated before entering a Bounded Context.

Example:

```text
External ERP SKU
      │
      ▼
ERP Adapter / ACL
      │
      ▼
Catalog Command
```

The ERP's model must not define Catalog's Domain model.

## 33. Context Boundary Tests

Architecture tests should verify:

```text
Catalog.Domain does not reference Pricing.Domain

Pricing.Domain does not reference Offers.Domain

Availability.Domain does not reference Fulfillment.Domain

Freight.Domain does not reference Availability.Domain

No Domain references Search Infrastructure

No Domain references AI provider SDKs
```

## 34. Consequences

### Positive

```text
clear ownership
smaller Domain models
independent evolution
reduced coupling
better scalability options
safer persistence choices
clearer team ownership
future microservice extraction
better event design
```

### Negative

```text
duplicate representations may exist
eventual consistency must be handled
cross-context workflows require orchestration
more contracts are required
developers must respect boundaries
```

These consequences are accepted.

## 35. Alternatives Considered

### Single Commerce Domain

Rejected because Product, Seller, Price, Availability and Freight have different rules and operational characteristics.

### One Context Per Entity

Rejected because Bounded Contexts represent cohesive business capabilities, not database tables.

### Microservice Per Entity

Rejected because this would create excessive distribution and coupling.

### Shared Enterprise Domain Model

Rejected because a universal model would couple contexts and weaken independent evolution.

## 36. Boundary Evolution

Bounded Context boundaries are architectural decisions, but they are not sacred forever.

They may evolve when real Domain knowledge reveals:

```text
incorrect ownership
excessive coupling
different lifecycle
different scaling requirements
new business capability
```

Changes must be documented by ADR.

## 37. Copilot Rules

GitHub Copilot must follow these rules:

```text
Do not create cross-context Domain references.

Do not move Product into Offers.

Do not move Price into Catalog.

Do not move Availability into Catalog.

Do not place Seller entities inside Offers.

Reference external context identities by ID/contracts.

Use Integration Events for asynchronous context communication.

Use explicit Application/API contracts for synchronous communication.

Do not create shared business entities in BuildingBlocks.

Do not access another context's database.

Do not merge contexts for implementation convenience.

Do not create a new Bounded Context without architectural justification.
```

## 38. Initial Context Summary

```text
Catalog
→ What is the product/SKU?

Sellers
→ Who is the seller?

Offers
→ Which seller commercially offers which SKU?

Pricing
→ At what price and payment condition is it sold?

Availability
→ Where and how much is sellable?

Fulfillment
→ Which logistical nodes can fulfill commerce?

Freight
→ How can it be delivered, at what cost and SLA?
```

## 39. Relationship to Other ADRs

This ADR depends on:

```text
ADR-0001
Use DDD, Clean Architecture and Hexagonal Architecture
```

It directly informs:

```text
ADR-0003
Database per Bounded Context

ADR-0004
Kafka for Event-Driven Integration

ADR-0005
Transactional Outbox

ADR-0007
Elasticsearch for Search Projections

ADR-0008
GenAI Provider Abstraction
```

## 40. Final Decision

Yunu.Commerce will use explicit Bounded Contexts for:

```text
Catalog
Sellers
Offers
Pricing
Availability
Fulfillment
Freight
```

Search and AI will consume and assist these capabilities without becoming owners of their canonical business state.

The defining principle is:

> A Bounded Context owns its language, rules, model and canonical data. Other contexts interact with it through explicit contracts, never by reaching inside its implementation.
