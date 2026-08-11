# Yunu.Commerce - Bounded Contexts

## 1. Purpose

This document defines the initial Domain-Driven Design boundaries of Yunu.Commerce.

The purpose of these boundaries is to establish:

* clear business ownership
* explicit domain responsibilities
* independent domain models
* controlled communication between domains
* independent data ownership
* replaceable infrastructure
* future service extraction boundaries

A Bounded Context represents a semantic boundary where a specific domain model and Ubiquitous Language apply.

Bounded Context boundaries are business boundaries.

They are not automatically deployment boundaries.

---

# 2. Initial Context Map

The initial Yunu.Commerce Bounded Contexts are:

```text
Yunu.Commerce

├── Catalog
├── Sellers
├── Offers
├── Pricing
├── Availability
├── Fulfillment
├── Freight
├── Search
├── AI
└── Integrations
```

These contexts collaborate but must not collapse into a single shared domain model.

---

# 3. Fundamental Ownership Rule

Every business concept must have one authoritative owner.

Examples:

```text
Catalog owns Product and SKU.

Sellers owns Seller.

Offers owns Offer.

Pricing owns Price.

Availability owns Availability.

Fulfillment owns FulfillmentNode.

Freight owns freight calculation concepts.

Search owns search projections.

AI owns AI orchestration and enrichment workflows.

Integrations owns translation between external systems and Yunu.Commerce.
```

Other contexts may reference identifiers or maintain projections of information they need.

They must not take ownership of another context's aggregate.

---

# 4. Context Relationships

High-level relationships:

```text
                         ┌─────────────┐
                         │   Catalog   │
                         └──────┬──────┘
                                │
                                │ Product / SKU
                                ▼
                         ┌─────────────┐
                         │   Offers    │
                         └──────┬──────┘
                                │
                 ┌──────────────┼──────────────┐
                 │              │              │
                 ▼              ▼              ▼
            ┌─────────┐   ┌──────────────┐ ┌─────────┐
            │ Pricing │   │ Availability │ │ Sellers │
            └─────────┘   └──────┬───────┘ └─────────┘
                                 │
                                 ▼
                          ┌─────────────┐
                          │ Fulfillment │
                          └──────┬──────┘
                                 │
                                 ▼
                            ┌─────────┐
                            │ Freight │
                            └─────────┘


          Events / APIs / Contracts
                    │
          ┌─────────┴──────────┐
          ▼                    ▼
      ┌────────┐            ┌──────┐
      │ Search │            │  AI  │
      └────────┘            └──────┘


                External Systems
                       │
                       ▼
                ┌──────────────┐
                │ Integrations │
                └──────────────┘
```

This diagram represents logical collaboration, not database or project references.

---

# 5. Catalog Bounded Context

## Responsibility

Catalog owns the canonical description and classification of products.

Catalog answers:

> What is this product?

Catalog does not answer:

> Who sells it?

> What does it cost?

> Is it currently available?

> How much does delivery cost?

Those questions belong to other contexts.

---

## Owned Concepts

Initial concepts include:

```text
Product
SKU
Category
Brand
ProductAttribute
Specification
Media
Catalog
CatalogItem
```

---

## Product

Product represents the conceptual commercial product.

Example:

```text
Product

Apple iPhone 17 Pro
```

Product may contain information such as:

```text
ProductId
Name
Description
Brand
Category
Attributes
Specifications
Media
Status
```

The final Aggregate design will be documented separately.

This document does not prescribe implementation details prematurely.

---

## SKU

SKU represents a specific sellable variation of a Product.

Example:

```text
Product
Apple iPhone 17 Pro

        │
        ├── SKU
        │   256 GB
        │   Black
        │
        ├── SKU
        │   512 GB
        │   Black
        │
        └── SKU
            256 GB
            Silver
```

Catalog owns SKU identity and descriptive characteristics.

Catalog does not own SKU price or inventory.

---

## Catalog Does Not Own

Catalog must not own:

```text
Seller
Offer
Price
Inventory
Availability
FreightQuote
Carrier
PaymentPrice
```

References to these concepts must occur through identifiers, contracts, projections or application composition.

---

## Potential Catalog Events

Examples:

```text
ProductCreated
ProductUpdated
ProductActivated
ProductDeactivated

SkuCreated
SkuUpdated
SkuActivated
SkuDeactivated

ProductCategorized
ProductMediaChanged
```

Exact events will be defined when domain behavior is implemented.

---

# 6. Sellers Bounded Context

## Responsibility

Sellers owns merchant identity and seller-specific business information.

Sellers answers:

> Who is selling through the platform?

---

## Owned Concepts

Potential concepts include:

```text
Seller
Merchant
SellerType
SellerStatus
SellerProfile
```

---

## First Party and Third Party

Yunu.Commerce supports:

```text
SellerType

├── FirstParty
└── ThirdParty
```

First Party represents the retailer or commerce operator selling its own inventory.

Third Party represents an external marketplace seller.

These are business concepts and must not depend on a specific marketplace implementation.

---

## Sellers Does Not Own

Sellers must not own:

```text
Product
SKU
Offer
Price
Availability
FreightQuote
```

---

## Potential Seller Events

Examples:

```text
SellerCreated
SellerActivated
SellerSuspended
SellerDeactivated
```

---

# 7. Offers Bounded Context

## Responsibility

Offers connects a sellable SKU with a Seller.

Offer answers:

> Who is selling this SKU?

An Offer represents the commercial existence of a SKU for a particular Seller.

---

## Core Relationship

```text
SKU
 │
 ├──────── Offer A
 │          Seller: First Party
 │
 ├──────── Offer B
 │          Seller: Seller X
 │
 └──────── Offer C
            Seller: Seller Y
```

---

## Potential Offer Concepts

```text
Offer
OfferId
OfferStatus
SellerId
SkuId
CommercialCondition
```

Offer references Seller and SKU by identity.

It must not contain copies of entire Seller or Product aggregates.

---

## Offer and Price

Offer does not own Pricing rules.

Conceptually:

```text
Offer
   │
   └── Pricing Context
```

Pricing may use:

```text
OfferId
SkuId
SellerId
Region
PaymentMethod
```

depending on the business rule.

The exact pricing model will be defined in the Pricing domain documentation.

---

## Offer and Availability

Offer does not own inventory.

Availability determines whether an Offer can currently be fulfilled.

Conceptually:

```text
Offer
   │
   └── Availability Context
```

---

## Potential Offer Events

Examples:

```text
OfferCreated
OfferActivated
OfferSuspended
OfferDeactivated
OfferChanged
```

---

# 8. Pricing Bounded Context

## Responsibility

Pricing owns monetary commercial conditions.

Pricing answers:

> How much does this Offer cost under these conditions?

---

## Required Capabilities

The model must be capable of supporting:

```text
National Price
Regional Price
Promotional Price
Seller Price
Payment-specific Price
PIX Price
Boleto Price
Credit Card Price
Installment Conditions
Price Validity Period
```

---

## Conceptual Pricing Model

```text
Offer
  │
  ├── Base / National Price
  │
  ├── Regional Price
  │       └── Region
  │
  ├── Payment Price
  │       ├── PIX
  │       ├── Boleto
  │       └── Credit Card
  │
  └── Promotional Conditions
```

This is conceptual only.

Aggregate boundaries will be defined during Pricing domain modeling.

---

## Money

Pricing must use explicit monetary concepts.

Potential Value Objects:

```text
Money
Currency
Percentage
Installment
PaymentMethod
PriceValidity
Region
```

Never model monetary business logic using floating-point types.

---

## Pricing Does Not Own

Pricing must not own:

```text
Product
SKU
Seller
Inventory
FulfillmentNode
FreightQuote
```

It may reference their identifiers when necessary.

---

## Potential Pricing Events

Examples:

```text
PriceCreated
PriceChanged
RegionalPriceChanged
PaymentPriceChanged
PromotionApplied
PriceExpired
```

---

# 9. Availability Bounded Context

## Responsibility

Availability determines whether products or offers can currently be fulfilled.

Availability answers:

> Can this item be fulfilled for this location?

Availability is not the Catalog.

Catalog describes the product.

Availability describes whether it can currently be supplied.

---

## Required Availability Scopes

The model must support:

```text
National
Regional
State
City
Fulfillment Node
Branch
```

Possible conceptual hierarchy:

```text
National Availability
        │
        ▼
Regional Availability
        │
        ▼
Local Availability
        │
        ▼
Fulfillment Node Availability
```

The actual hierarchy must remain configurable and must not assume every retailer organizes regions identically.

---

## Potential Concepts

```text
Availability
StockPosition
AvailableQuantity
AvailabilityStatus
Region
ServiceArea
FulfillmentNodeId
```

Stock and Availability must not automatically be considered identical concepts.

Inventory may represent physical quantity.

Availability may represent the business decision that an item can be sold or fulfilled.

This distinction must be preserved during domain modeling.

---

## Availability Does Not Own

Availability must not own:

```text
Product
Seller
Price
FreightQuote
FulfillmentNode
```

It references identities owned by other contexts.

---

## Potential Availability Events

Examples:

```text
AvailabilityChanged
NationalAvailabilityChanged
RegionalAvailabilityChanged
BranchAvailabilityChanged
StockPositionChanged
```

---

# 10. Fulfillment Bounded Context

## Responsibility

Fulfillment owns the canonical representation of physical or logical fulfillment locations.

Fulfillment answers:

> From where can an order potentially be fulfilled?

---

## Potential Concepts

```text
FulfillmentNode
Store
Branch
Warehouse
DistributionCenter
FulfillmentCenter
ServiceArea
FulfillmentCapability
```

Rather than forcing every external system into a specific retail concept, the canonical abstraction should support different fulfillment models.

---

## Fulfillment Node

`FulfillmentNode` is the canonical concept representing a location capable of participating in fulfillment.

A node may represent:

```text
Physical Store
Warehouse
Distribution Center
Dark Store
Marketplace Seller Warehouse
Pickup Location
```

Node capabilities may differ.

Examples:

```text
Shipping
Pickup
StockHolding
SameDayDelivery
ExpressDelivery
```

Exact modeling will be defined later.

---

## Fulfillment Does Not Own

Fulfillment must not own:

```text
Product
SKU
Offer
Price
Inventory
FreightQuote
```

---

## Potential Fulfillment Events

Examples:

```text
FulfillmentNodeCreated
FulfillmentNodeActivated
FulfillmentNodeDeactivated
FulfillmentCapabilityChanged
ServiceAreaChanged
```

---

# 11. Freight Bounded Context

## Responsibility

Freight owns delivery calculation and delivery options.

Freight answers:

> How can this Offer reach this destination?

and:

> What will delivery cost and when can it arrive?

---

## Potential Concepts

```text
FreightQuote
FreightOption
DeliveryMethod
Carrier
ServiceLevel
DeliveryPromise
Destination
ServiceArea
FreightCost
```

---

## Example

```text
Offer
  │
  ▼
Destination
CEP 11000-000
  │
  ├── Standard
  │     R$ 14.90
  │     3 business days
  │
  ├── Express
  │     R$ 28.90
  │     1 business day
  │
  └── Pickup
        Free
        Available today
```

---

## External Freight Providers

Freight calculation providers are external infrastructure.

They must be accessed through Ports and Adapters.

Conceptually:

```text
Freight Application
       │
       ▼
IFreightProvider
       │
       ├──── Carrier A Adapter
       ├──── Carrier B Adapter
       └──── External Logistics Adapter
```

External freight models must not leak into the Freight Domain.

---

## Potential Freight Events

Examples:

```text
FreightQuoteCalculated
DeliveryPromiseChanged
FreightPolicyChanged
```

Not every freight calculation requires an integration event.

Events must exist only when another part of the system has a meaningful reason to react.

---

# 12. Search Context

## Responsibility

Search provides optimized product discovery.

Search is primarily a read-oriented context.

It consumes information produced by other contexts and creates search projections.

---

## Search Projection

A search document may contain denormalized information such as:

```text
ProductId
SkuId
ProductName
Brand
Category
Attributes
Seller
Offer
LowestPrice
Availability
Regions
SearchKeywords
Embedding
```

This duplication is intentional.

Search projections optimize reads.

They do not transfer domain ownership to Search.

---

## Search Data Flow

```text
Catalog ─────────────┐
Offers ──────────────┤
Pricing ─────────────┤
Availability ────────┤
Sellers ─────────────┤
                     ▼
                   Kafka
                     │
                     ▼
              Search Projection
                     │
                     ▼
              Elasticsearch
```

---

## Search Does Not Own

Search does not own authoritative:

```text
Products
Offers
Prices
Availability
Seller information
```

Search contains projections.

---

# 13. AI Context

## Responsibility

AI owns AI orchestration and AI-specific workflows.

AI must not become the owner of commerce data.

---

## Potential Responsibilities

```text
Catalog Enrichment
Prompt Orchestration
Model Routing
Tool Execution
RAG
Embeddings
Semantic Retrieval
AI Guardrails
AI Usage Tracking
AI Cost Tracking
AI Evaluation
```

---

## AI Provider Independence

AI must communicate with providers through abstractions.

```text
AI Application
      │
      ▼
IGenerativeAIProvider
      │
      ├── Azure OpenAI Adapter
      └── Google Vertex AI Adapter
```

Provider-specific objects must remain in Infrastructure.

---

## AI and Commerce Data

AI must access current commerce information through explicit tools or application contracts.

Forbidden:

```text
AI Agent
   │
   ▼
SELECT * FROM PricingDatabase
```

Preferred:

```text
AI Agent
   │
   ▼
get_price
   │
   ▼
Pricing Application
```

---

## Catalog Enrichment

AI may propose catalog enrichment.

Conceptual ownership:

```text
Catalog owns Product.

AI owns enrichment execution.

Catalog decides whether enrichment becomes Product state.
```

This distinction is critical.

AI must not become an alternative Catalog database.

---

# 14. Integrations Context

## Responsibility

Integrations protects Yunu.Commerce from external models.

It implements:

* Adapters
* Translators
* External API clients
* Anti-Corruption Layers
* Import pipelines
* Export pipelines
* External event translation

---

## Example

External ERP:

```text
MATNR
WERKS
VKORG
```

must not become internal domain terminology.

Instead:

```text
External ERP Model
       │
       ▼
ERP Anti-Corruption Layer
       │
       ▼
Yunu Canonical Contract
       │
       ▼
Catalog / Fulfillment / Offers
```

---

## Integration Rule

External systems adapt to Yunu.Commerce.

Yunu.Commerce Domain must not adapt itself to every external system.

---

# 15. Cross-Context Communication

Bounded Contexts may communicate through:

```text
Integration Events
Application APIs
Explicit Contracts
Read Models
```

They must not communicate through:

```text
Shared database tables
Direct access to another context's database
References to another context's Infrastructure project
Shared mutable domain entities
```

---

# 16. Identifier References

A context may reference the identity of an aggregate owned by another context.

Example:

```text
Offer

OfferId
SkuId
SellerId
```

This does not mean Offer owns SKU or Seller.

It only holds their identities.

Avoid object graphs crossing Bounded Context boundaries.

Forbidden conceptual model:

```text
Offer
 ├── Product
 │    └── SKU
 └── Seller
      └── Complete Seller Aggregate
```

Preferred:

```text
Offer
 ├── SkuId
 └── SellerId
```

---

# 17. Data Ownership

Each Bounded Context owns its persistence.

Conceptually:

```text
Catalog
   └── Catalog Data

Sellers
   └── Seller Data

Offers
   └── Offer Data

Pricing
   └── Pricing Data

Availability
   └── Availability Data

Fulfillment
   └── Fulfillment Data

Search
   └── Search Indexes

AI
   └── AI-specific operational data
```

This does not require one physical database server per context.

Physical infrastructure may be shared.

Logical ownership must not be shared.

---

# 18. Physical Database vs Logical Ownership

Multiple contexts may initially use the same database technology or even the same database server.

Example:

```text
MongoDB Cluster

├── catalog database
├── availability database
└── ai-projections database
```

or:

```text
SQL Server

├── Sellers schema/database
├── Pricing schema/database
└── Fulfillment schema/database
```

The critical rule is that each context owns its schema/data boundary.

Sharing infrastructure does not imply sharing domain ownership.

---

# 19. Synchronous vs Asynchronous Communication

Use synchronous communication when the caller requires an immediate answer.

Examples:

```text
Get current price
Calculate freight
Check current availability
```

Use asynchronous communication when other contexts react to facts.

Examples:

```text
ProductCreated
PriceChanged
AvailabilityChanged
SellerActivated
```

Do not use Kafka as a replacement for every synchronous interaction.

Do not use synchronous APIs for every integration event.

Choose based on semantics.

---

# 20. Consistency Model

Strong consistency should normally exist inside an Aggregate boundary.

Cross-context operations should generally assume eventual consistency.

Example:

```text
Catalog updates Product
        │
        ▼
ProductUpdated
        │
        ▼
Kafka
        │
        ▼
Search projection updated
```

There may be a short period where Search contains the previous representation.

That is acceptable if documented by the use case.

---

# 21. No Distributed Aggregate

An Aggregate must not span multiple Bounded Contexts.

Forbidden:

```text
CommerceAggregate

├── Product
├── Seller
├── Offer
├── Price
├── Inventory
└── Freight
```

This creates distributed transactional coupling.

Each context owns its own transactional consistency boundary.

---

# 22. Shared Kernel Policy

Shared Kernel must remain extremely small.

Potential truly shared technical/domain primitives may include carefully evaluated concepts such as:

```text
CorrelationId
Event metadata abstractions
certain identifier infrastructure
```

However, business concepts should not automatically be moved into Shared Kernel simply because multiple contexts use similar words.

For example:

```text
Region
Money
Address
```

may have different semantics in different contexts.

The preferred rule is:

> Duplicate a small concept before creating incorrect domain coupling.

Shared Kernel additions require explicit architectural justification.

---

# 23. Contracts

Integration contracts must be separate from internal Domain models.

Example:

```text
Catalog.Domain.Product
```

must not automatically become:

```text
ProductCreatedIntegrationEvent
```

Integration events and API contracts should expose only the information required by consumers.

Internal domain refactoring must not automatically break external contracts.

---

# 24. Context Independence

Each context should eventually be capable of independent evolution.

This means:

* its Domain is independent
* its persistence is owned
* its external contracts are explicit
* its infrastructure is replaceable
* its events are versioned
* its internal model is not shared

This prepares Yunu.Commerce for future service extraction without forcing premature microservices.

---

# 25. Deployment Boundaries

A Bounded Context does not automatically equal one executable.

Initially, Yunu.Commerce may deploy multiple contexts together where operationally convenient.

Example:

```text
Logical Architecture

Catalog
Offers
Pricing
Availability
```

could initially exist in fewer deployment units while preserving strict module boundaries.

Later:

```text
Catalog Service

Pricing Service

Availability Service
```

may become independently deployable.

No domain redesign should be necessary merely because the deployment topology changes.

---

# 26. Context Dependency Direction

Contexts should avoid circular dependencies.

Example of undesirable coupling:

```text
Catalog → Offers
Offers → Pricing
Pricing → Catalog
```

Prefer event contracts and explicit application interfaces that keep ownership directional and clear.

A context must not require another context's Domain assembly merely to reference its concepts.

Use identifiers and contracts.

---

# 27. Canonical Model

Yunu.Commerce defines a canonical commerce language.

External systems translate into that language.

Core canonical concepts currently include:

```text
Product
SKU
Seller
Offer
Price
Availability
FulfillmentNode
FreightQuote
```

These names describe Yunu.Commerce concepts.

Their internal definitions are controlled by their owning Bounded Context.

---

# 28. Context Evolution

These boundaries are the initial architecture.

DDD boundaries may evolve when real domain knowledge demonstrates that a boundary is incorrect.

Changes must be deliberate.

A major Bounded Context change should:

1. identify the discovered domain problem
2. describe the previous boundary
3. describe the proposed boundary
4. evaluate data ownership
5. evaluate events and contracts
6. evaluate migration impact
7. create an Architecture Decision Record

Copilot must not casually move concepts between contexts.

---

# 29. Initial Implementation Priority

Bounded Contexts will not all be implemented simultaneously.

Initial implementation should focus on a vertical slice.

Recommended order:

```text
Catalog
   ↓
AI Enrichment
   ↓
Search
```

followed by:

```text
Sellers
   ↓
Offers
   ↓
Pricing
   ↓
Availability
   ↓
Fulfillment
   ↓
Freight
```

This ordering is an implementation strategy, not a statement that later contexts are less important.

---

# 30. Initial Catalog Vertical Slice

The first functional slice should validate:

```text
Create Product
      │
      ▼
Create SKU
      │
      ▼
Persist Catalog
      │
      ▼
Publish Event
      │
      ▼
AI Enrichment
      │
      ▼
Catalog Approval / Update
      │
      ▼
Search Projection
      │
      ▼
Elasticsearch
      │
      ▼
Search Product
```

This slice exercises:

* DDD
* Clean Architecture
* Hexagonal Architecture
* CQRS
* EDA
* persistence
* Kafka
* AI abstraction
* Elasticsearch

without requiring every commerce domain to be implemented first.

---

# 31. Context Ownership Summary

| Concept                     | Owning Context                   |
| --------------------------- | -------------------------------- |
| Product                     | Catalog                          |
| SKU                         | Catalog                          |
| Category                    | Catalog                          |
| Brand                       | Catalog                          |
| Seller                      | Sellers                          |
| Seller Type                 | Sellers                          |
| Offer                       | Offers                           |
| National Price              | Pricing                          |
| Regional Price              | Pricing                          |
| Payment Price               | Pricing                          |
| Availability                | Availability                     |
| Stock Position              | Availability                     |
| Fulfillment Node            | Fulfillment                      |
| Branch                      | Fulfillment                      |
| Warehouse                   | Fulfillment                      |
| Freight Quote               | Freight                          |
| Delivery Option             | Freight                          |
| Search Document             | Search                           |
| Search Embedding            | Search / AI integration boundary |
| AI Enrichment               | AI                               |
| Prompt Execution            | AI                               |
| External System Translation | Integrations                     |

This table defines ownership, not necessarily the final Aggregate structure.

---

# 32. Architectural Rule

The primary rule governing Bounded Contexts is:

> A context may know another context exists, but it must not become dependent on that context's internal model.

Communication happens through explicit boundaries.

Data ownership remains local.

Business behavior remains with the context that owns the concept.

Infrastructure does not define domain boundaries.
