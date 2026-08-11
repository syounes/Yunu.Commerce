# Yunu.Commerce - System Overview

## 1. Purpose

Yunu.Commerce is a modular, enterprise-grade commerce platform designed to manage the core capabilities required by modern e-commerce ecosystems.

The platform provides a canonical commerce model for managing:

* Products
* SKUs
* Catalogs
* Categories
* Brands
* Product attributes
* Sellers
* First-party commerce (1P)
* Third-party commerce (3P)
* Offers
* National pricing
* Regional pricing
* Payment-specific pricing
* Availability
* Regional availability
* Branch availability
* Fulfillment
* Freight
* Search
* Generative AI capabilities
* External integrations

The platform is designed as a reusable product and must not contain business logic tied to a specific retailer, marketplace, ERP, cloud provider or customer.

---

# 2. Product Vision

Yunu.Commerce must provide a reusable commerce foundation capable of supporting different retailers and commerce scenarios without redesigning its core domain.

The platform should allow external organizations to integrate their existing systems while preserving the Yunu.Commerce canonical domain model.

External systems may include:

* ERP
* OMS
* WMS
* PIM
* marketplaces
* payment systems
* logistics providers
* pricing engines
* inventory systems
* legacy systems
* external databases
* partner APIs

External models must be translated into the Yunu.Commerce canonical model through integration adapters and Anti-Corruption Layers.

The platform must evolve independently from external systems.

---

# 3. Architectural Goals

The architecture must optimize for:

* Domain isolation
* Modularity
* Maintainability
* Scalability
* Testability
* Observability
* Resilience
* Extensibility
* Infrastructure replaceability
* Cloud portability
* Integration flexibility
* AI provider independence
* Long-term maintainability

The system must be capable of evolving from a modular architecture into independently deployable distributed services without redesigning the business domain.

---

# 4. Architectural Styles

Yunu.Commerce combines multiple architectural approaches.

## Domain-Driven Design

DDD defines:

* Bounded Contexts
* Aggregates
* Aggregate Roots
* Entities
* Value Objects
* Domain Services
* Domain Events
* Business invariants
* Ubiquitous Language

Business concepts must drive the architecture.

---

## Clean Architecture

Clean Architecture defines dependency direction and isolation between business logic and infrastructure.

The fundamental dependency direction is:

```text
Infrastructure
      ↓
Application
      ↓
Domain
```

Dependencies must always point toward the business core.

Infrastructure must never become a dependency of the Domain.

---

## Hexagonal Architecture

Hexagonal Architecture defines the interaction between the application and external technologies through Ports and Adapters.

Examples:

```text
Application / Domain
        │
       Ports
        │
     Adapters
        │
External Technology
```

External technologies include:

* databases
* message brokers
* search engines
* cache providers
* AI providers
* external APIs
* cloud services
* ERP systems
* logistics providers

---

## Event-Driven Architecture

EDA enables asynchronous communication and loose coupling between Bounded Contexts.

Kafka is the initial event streaming platform.

Example:

```text
Catalog
   │
   │ ProductUpdated
   ▼
Kafka
   │
   ├────────► Search
   ├────────► AI
   ├────────► Analytics
   └────────► Integrations
```

The producer does not need to know which consumers process the event.

---

## CQRS

Commands and Queries represent different application intentions.

Commands modify state.

Queries retrieve state.

Examples:

```text
CreateProductCommand
CreateSkuCommand
ChangePriceCommand
UpdateAvailabilityCommand
```

and:

```text
GetProductQuery
SearchProductsQuery
GetPriceQuery
GetAvailabilityQuery
```

CQRS must be used pragmatically and must not introduce unnecessary complexity.

---

# 5. High-Level Business Capabilities

The initial Yunu.Commerce platform contains the following major capabilities.

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

Each capability represents an independent business responsibility.

The detailed boundaries are documented in:

```text
docs/architecture/02-bounded-contexts.md
```

---

# 6. Catalog

Catalog manages the canonical representation of products sold through the platform.

Primary concepts include:

```text
Product
SKU
Category
Brand
Attribute
Specification
Media
Catalog
CatalogItem
```

A Product represents the conceptual commercial product.

A SKU represents a specific sellable variation.

Example:

```text
Product

Apple iPhone 17 Pro

        │
        ├── SKU
        │   256 GB / Black
        │
        ├── SKU
        │   512 GB / Black
        │
        └── SKU
            256 GB / Silver
```

Product and SKU must not contain infrastructure-specific information.

---

# 7. Sellers

The platform supports both first-party and third-party commerce.

```text
Seller
├── First Party (1P)
└── Third Party (3P)
```

First Party represents inventory or offers owned directly by the retailer operating the platform.

Third Party represents marketplace sellers.

Seller concepts must remain independent from specific marketplace implementations.

---

# 8. Offers

Product identity and commercial offers are different concepts.

A SKU identifies what is being sold.

An Offer identifies who is selling it and under which commercial conditions.

```text
Product
   │
   ▼
SKU
   │
   ├────────► Offer - Seller A - 1P
   │
   ├────────► Offer - Seller B - 3P
   │
   └────────► Offer - Seller C - 3P
```

A single SKU may therefore have multiple Offers.

Offer may reference:

* Seller
* SKU
* Commercial status
* Pricing
* Availability
* Fulfillment conditions

Pricing and availability remain owned by their respective domains.

---

# 9. Pricing

Pricing manages commercial price information.

The architecture must support:

* National price
* Regional price
* Promotional price
* Seller-specific price
* Payment-specific price
* PIX price
* Boleto price
* Credit card price
* Installment conditions
* Price validity periods

Example:

```text
SKU
 │
 └── Offer
      │
      ├── National Price
      │      R$ 5,000
      │
      ├── Regional Price - SP
      │      R$ 4,800
      │
      ├── PIX
      │      R$ 4,500
      │
      ├── Boleto
      │      R$ 4,650
      │
      └── Credit Card
             R$ 5,000
             10 installments
```

Money must be represented through explicit domain concepts rather than raw floating-point primitives.

---

# 10. Availability

Availability determines whether an Offer or SKU can be fulfilled for a particular location.

The platform must support multiple availability scopes.

```text
National
    │
    ▼
Regional
    │
    ▼
State
    │
    ▼
City
    │
    ▼
Branch / Fulfillment Node
```

Possible concepts include:

```text
NationalAvailability
RegionalAvailability
BranchAvailability
StockPosition
```

Availability must remain separate from Catalog.

Catalog describes the product.

Availability describes whether the product can currently be fulfilled.

---

# 11. Fulfillment

Fulfillment represents the physical nodes capable of storing, shipping or delivering products.

Possible fulfillment nodes include:

```text
Store
Branch
Warehouse
Distribution Center
Fulfillment Center
Marketplace Seller Location
```

The canonical concept is:

```text
FulfillmentNode
```

A Fulfillment Node may participate in:

* inventory
* delivery
* pickup
* regional availability
* freight calculation

---

# 12. Freight

Freight determines delivery possibilities for an Offer.

Primary concepts may include:

```text
FreightQuote
FreightOption
DeliveryMethod
Carrier
ServiceLevel
DeliveryPromise
ServiceArea
```

Example:

```text
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

Freight providers are infrastructure integrations and must be accessed through adapters.

---

# 13. Search

Search provides optimized product discovery.

Elasticsearch is the initial search technology.

Elasticsearch must be treated as a derived read model and must not automatically become the source of truth.

Typical flow:

```text
Catalog
    │
    │ ProductUpdated
    ▼
Kafka
    │
    ▼
Search Indexer
    │
    ▼
Elasticsearch
```

Search documents may combine information from:

* Catalog
* Offers
* Pricing
* Availability
* Sellers

Search models are optimized for queries rather than domain persistence.

---

# 14. Generative AI

Generative AI is a platform capability and must not be coupled to a specific provider.

Initial supported provider strategies may include:

```text
Azure OpenAI
Google Vertex AI
```

The platform must expose provider-independent abstractions.

Conceptual architecture:

```text
Yunu.Commerce
      │
      ▼
AI Application Layer
      │
      ▼
AI Provider Port
      │
      ├────────► Azure OpenAI Adapter
      │
      └────────► Google Vertex AI Adapter
```

Changing the AI provider must not require changing business domains.

---

# 15. AI Catalog Enrichment

One of the first AI capabilities will be product catalog enrichment.

Possible operations include:

* Product title normalization
* Product description generation
* Attribute extraction
* Category classification
* Technical specification extraction
* SEO metadata generation
* Keyword generation
* Tag generation
* FAQ generation
* Data quality analysis
* Product inconsistency detection

Conceptual flow:

```text
Product Imported
       │
       ▼
Catalog
       │
       │ ProductCreated
       ▼
Kafka
       │
       ▼
AI Enrichment Worker
       │
       ▼
Generative AI Provider
       │
       ▼
Proposed Product Enrichment
       │
       ▼
Validation / Approval
       │
       ▼
Catalog Update
```

AI-generated information must not silently replace source-of-truth data.

---

# 16. AI Commerce Agents

Future AI capabilities may provide conversational commerce.

Example customer request:

```text
"I need a notebook for .NET development,
Docker and Kubernetes,
under R$ 8,000,
available in Santos,
with delivery tomorrow."
```

The LLM must not invent commerce information.

Instead, the agent must interact with explicit commerce tools.

Example:

```text
Customer
   │
   ▼
Commerce AI Agent
   │
   ├──── search_products
   │
   ├──── get_product
   │
   ├──── get_price
   │
   ├──── get_availability
   │
   └──── get_freight
   │
   ▼
Commerce Services
```

The LLM provides reasoning and natural-language interaction.

Commerce services remain the source of truth.

---

# 17. Retrieval-Augmented Generation

RAG may use commerce information such as:

* Product descriptions
* Product specifications
* Categories
* Manuals
* FAQs
* Seller information
* Commercial policies
* Catalog metadata

Embeddings may be generated for semantic retrieval.

The vector infrastructure must remain replaceable.

Possible implementation technologies include:

* Elasticsearch vector search
* dedicated vector databases
* cloud vector services

Provider-specific vector types must not leak into Domain or Application contracts.

---

# 18. Hybrid Search

Product discovery may combine:

```text
Lexical Search
+
Semantic Search
+
Business Ranking
```

Possible ranking signals include:

* textual relevance
* vector similarity
* availability
* price
* seller quality
* 1P / 3P strategy
* promotions
* business rules

AI must complement deterministic commerce rules rather than replace them.

---

# 19. Integration Architecture

External systems interact with Yunu.Commerce through explicit integration boundaries.

Example:

```text
External ERP

MATNR
WERKS
VKORG

      │
      ▼

ERP Adapter
      │
      ▼

Yunu Canonical Model

Product
SKU
Branch
Seller
Offer
```

External terminology must not leak into the core domain.

This is enforced through Anti-Corruption Layers.

---

# 20. Event Streaming

Kafka is the initial asynchronous communication backbone.

Possible events include:

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

Integration Events describe facts that already occurred.

Events must be versioned and designed for backward compatibility.

---

# 21. Transactional Messaging

Yunu.Commerce must avoid unsafe dual writes.

When a business operation requires both:

```text
Database update
+
Event publication
```

the architecture should use the Transactional Outbox Pattern where appropriate.

```text
Business Transaction
       │
       ├── Aggregate Changes
       │
       └── Outbox Message
               │
             COMMIT
               │
               ▼
        Outbox Processor
               │
               ▼
             Kafka
```

Consumers must be idempotent.

---

# 22. Data Architecture

Yunu.Commerce uses Polyglot Persistence.

No single database technology is required to solve every problem.

Initial technology candidates:

```text
Relational Data
    SQL Server / PostgreSQL / Azure SQL

Document Data
    MongoDB

Distributed Cache
    Redis

Search
    Elasticsearch

Event Streaming
    Kafka

Object Storage
    Cloud Object Storage

Vector Search
    Elasticsearch Vector Search
    or replaceable Vector Store
```

Technology selection must follow domain and operational requirements.

---

# 23. Data Ownership

Each Bounded Context owns its data.

Direct database access across Bounded Contexts is prohibited.

Forbidden:

```text
Pricing
   │
   ▼
Catalog Database
```

Allowed:

```text
Catalog
   │
   │ ProductUpdated
   ▼
Kafka
   │
   ▼
Pricing Projection
```

or, when synchronous information is required:

```text
Pricing
   │
   ▼
Catalog Application API
```

Database schemas are private implementation details of their owning context.

---

# 24. Cache Architecture

Redis may be used for high-performance read scenarios.

Potential use cases include:

* Pricing cache
* Availability cache
* Freight cache
* Search cache
* Distributed locks
* Rate limiting
* AI semantic cache

Redis must not become the authoritative source for business data unless explicitly documented by an architectural decision.

Cache invalidation strategy must follow the characteristics of each domain.

---

# 25. Cloud Architecture

Yunu.Commerce must remain cloud-portable at the business architecture level.

Initial production infrastructure may use Azure.

Potential Azure components include:

```text
Azure Kubernetes Service
Azure Container Registry
Azure Key Vault
Azure Monitor
Application Insights
Microsoft Entra ID
Azure API Management
Azure OpenAI
```

Alternative providers may be introduced through infrastructure adapters.

Cloud SDK types must not leak into Domain or Application contracts.

---

# 26. Container Architecture

Development and deployment must support containerized workloads.

Local development may use:

```text
Docker
Docker Compose
```

Local infrastructure may include:

```text
MongoDB
SQL
Kafka
Redis
Elasticsearch
OpenTelemetry Collector
```

Production workloads may run on Kubernetes.

---

# 27. Observability

Observability is a first-class architecture concern.

The platform must support:

```text
Logs
Metrics
Distributed Traces
```

OpenTelemetry is the preferred vendor-neutral observability standard.

Distributed operations should preserve:

```text
TraceId
CorrelationId
CausationId
```

Observability must cover:

* APIs
* Application handlers
* Database operations
* Kafka producers
* Kafka consumers
* External integrations
* AI requests
* Search indexing
* Background workers

---

# 28. Security

Security must be designed from the beginning.

Potential production capabilities include:

```text
Microsoft Entra ID
OAuth 2.0
OpenID Connect
JWT
Managed Identity
Workload Identity
RBAC
Azure Key Vault
API Management
Network Security
Private Endpoints
```

Secrets must never be stored in source code.

Security infrastructure must remain outside the Domain.

---

# 29. Resilience

Distributed operations must assume partial failure.

Depending on the scenario, the platform may use:

```text
Timeout
Retry
Circuit Breaker
Rate Limiting
Bulkhead
Fallback
Dead Letter Queue
Idempotency
Health Checks
```

Retry policies must respect operation idempotency.

---

# 30. System Context

Conceptually, the platform sits between commerce channels and enterprise systems.

```text
                    Commerce Channels

          Web / Mobile / Marketplace / B2B
                         │
                         ▼
                 API Gateway / APIM
                         │
                         ▼
                 Yunu.Commerce
                         │
        ┌────────────────┼────────────────┐
        │                │                │
        ▼                ▼                ▼
 Commerce Domains    Search / AI      Integrations
        │                │                │
        ▼                ▼                ▼
 SQL / MongoDB     Elastic / Redis    ERP / OMS / WMS
        │                │                │
        └────────────────┼────────────────┘
                         │
                         ▼
                       Kafka
```

---

# 31. Modular-First Strategy

Yunu.Commerce must begin with strong modular boundaries.

A Bounded Context does not automatically require an independently deployed microservice.

Initial architecture may combine modules operationally while preserving logical boundaries.

This avoids premature distributed-system complexity.

The architecture must allow modules to become independent services when justified by:

* scalability
* deployment independence
* ownership
* performance
* reliability
* security
* operational requirements

The rule is:

> Domain boundaries are business decisions. Deployment boundaries are operational decisions.

---

# 32. Infrastructure Replaceability

The architecture must assume infrastructure technologies can change.

Examples:

```text
MongoDB
    ↕
Alternative Document Store

SQL Server
    ↕
PostgreSQL

Redis
    ↕
Alternative Distributed Cache

Elasticsearch
    ↕
Alternative Search Engine

Kafka
    ↕
Alternative Event Broker

Azure OpenAI
    ↕
Google Vertex AI
```

Replacing infrastructure must not require redesigning the business domain.

---

# 33. Initial Development Strategy

The project will be implemented incrementally.

The first stage creates the architecture skeleton.

```text
Stage 1
Architecture Documentation

        ↓

Stage 2
.NET Solution Skeleton

        ↓

Stage 3
Domain Foundations

        ↓

Stage 4
Application Use Cases

        ↓

Stage 5
Infrastructure Adapters

        ↓

Stage 6
Event Architecture

        ↓

Stage 7
Catalog MVP

        ↓

Stage 8
Generative AI Integration

        ↓

Stage 9
Search / RAG / Agents

        ↓

Stage 10
Cloud Deployment
```

Each stage must compile and remain architecturally valid before significant new capabilities are introduced.

---

# 34. Initial Functional Milestone

The first functional milestone is intentionally small.

The platform must be capable of:

1. Receiving product information.
2. Creating a canonical Product.
3. Creating one or more SKUs.
4. Persisting Catalog information.
5. Publishing appropriate events.
6. Enriching product information through Generative AI.
7. Validating the generated enrichment.
8. Updating the Catalog.
9. Indexing the product for search.
10. Retrieving the resulting product.

This vertical slice will validate the architecture from Domain to AI and Search.

---

# 35. Architectural Principle

The fundamental architectural principle of Yunu.Commerce is:

> The business model owns the architecture. Infrastructure serves the business model.

Technologies such as:

* Azure
* Google Cloud
* MongoDB
* SQL Server
* Kafka
* Redis
* Elasticsearch
* Generative AI providers

are implementation details.

They may evolve or be replaced.

The canonical Yunu.Commerce business model must remain stable and independent from those technologies.
