# Yunu.Commerce - Guia de Estudo de Arquitetura

## 1. Resumo Executivo

Yunu.Commerce é uma plataforma modular de e-commerce desenhada com:

- Domain-Driven Design (DDD)
- Clean Architecture
- Hexagonal Architecture
- Event-Driven Architecture (EDA)
- Polyglot Persistence
- Generative AI
- Cloud-native deployment

O objetivo é construir uma arquitetura que possa evoluir sem acoplar o núcleo de negócio a databases, brokers, cloud providers ou AI vendors.

A ideia central é:

> Business rules permanecem estáveis. Infrastructure é substituível.

---

# 2. Por Que Estou Construindo Dessa Forma

A plataforma precisa suportar:

- Products
- SKUs
- Sellers
- comércio 1P e 3P
- Offers
- National Pricing e Regional Pricing
- PIX Pricing e Boleto Pricing
- Availability
- Availability por Branch / Store / Distribution Center
- Fulfillment
- Regional Freight
- Search
- Generative AI

Se tudo isso fosse colocado dentro de um único modelo de aplicação, o sistema ficaria rapidamente muito acoplado.

A arquitetura separa responsabilidades para que cada capability possa evoluir de forma independente.

---

# 3. Arquitetura em Uma Frase

> Yunu.Commerce usa DDD para modelar o negócio, Clean Architecture para controlar a direção das dependências, Hexagonal Architecture para isolar tecnologia e EDA/Kafka para integrar Bounded Contexts de forma assíncrona.

---

# 4. DDD - Por Quê

DDD é usado porque o sistema possui conceitos de negócio complexos e diferentes áreas de responsabilidade.

DDD ajuda a definir:

- Bounded Contexts
- Aggregates
- Entities
- Value Objects
- Domain Services
- Domain Events
- Ubiquitous Language

DDD responde:

> Como o negócio deve ser modelado?

---

# 5. Clean Architecture - Por Quê

Clean Architecture protege o núcleo de negócio das dependências técnicas.

Direção das dependências:

```text
Infrastructure
     ↓
Application
     ↓
Domain
```

O Domain não conhece:

- MongoDB
- SQL
- Kafka
- Redis
- Elasticsearch
- Azure
- Google AI
- HTTP
- ASP.NET Core

Clean Architecture responde:

> Em qual direção as dependências podem apontar?

---

# 6. Hexagonal Architecture - Por Quê

Hexagonal Architecture isola tecnologias externas usando Ports e Adapters.

Exemplo:

```text
Application
    ↓
IGenerativeAiProvider
    ↓
Azure AI Adapter
ou
Google AI Adapter
```

A mesma ideia vale para:

- databases
- Kafka
- Redis
- Elasticsearch
- carriers
- object storage

Hexagonal Architecture responde:

> Como substituir Infrastructure sem alterar business logic?

---

# 7. Event-Driven Architecture - Por Quê

EDA é usada porque diferentes Bounded Contexts precisam reagir a mudanças de negócio de forma independente.

Exemplo:

```text
PriceChanged
    ↓
Kafka
    ├─ Search atualiza Elasticsearch
    ├─ Cache atualiza Redis
    └─ Analytics pode consumir depois
```

Isso reduz acoplamento direto.

EDA responde:

> Como módulos independentes se comunicam de forma assíncrona?

---

# 8. Bounded Contexts

Os Bounded Contexts iniciais de negócio são:

```text
Catalog
Sellers
Offers
Pricing
Availability
Fulfillment
Freight
```

Capabilities de suporte:

```text
Search
AI
Integrations
```

---

# 9. Catalog - Por Que Separado

Catalog responde:

> O que é o Product?

É responsável por:

- Product
- SKU
- Brand
- Category
- Attributes
- Specifications
- Media

Catalog não é responsável por:

- Price
- Seller
- Stock
- Freight

Motivo:

Product identity e dados descritivos possuem lifecycle diferente dos dados comerciais e operacionais.

---

# 10. Sellers - Por Que Separado

Sellers responde:

> Quem está vendendo?

É responsável por:

- Seller
- SellerId
- Seller Type
- 1P / 3P
- Seller Status
- Seller lifecycle

Motivo:

Seller identity precisa evoluir independentemente de Product e Offer.

---

# 11. Offers - Por Que Separado

Offers responde:

> Qual Seller está oferecendo qual SKU?

Conceitualmente:

```text
Seller + SKU = Offer
```

É responsável por:

- OfferId
- SellerId
- SkuId
- Offer lifecycle

Motivo:

O mesmo SKU pode ser vendido por vários Sellers.

---

# 12. Pricing - Por Que Separado

Pricing responde:

> Quanto custa este Offer?

É responsável por:

- National Price
- Regional Price
- Sale Price
- PIX Price
- Boleto Price
- Credit Card conditions
- Validity
- Price lifecycle

Motivo:

Pricing possui regras próprias, histórico, precisão financeira e condições regionais/de pagamento.

---

# 13. Availability - Por Que Separado

Availability responde:

> Este item pode ser vendido agora, onde e em qual quantidade?

É responsável por:

- Sellable Quantity
- National Availability
- Regional Availability
- Fulfillment Node Availability
- Availability Status

Motivo:

Availability possui frequência de atualização muito alta e características de escalabilidade diferentes de Catalog.

---

# 14. Fulfillment - Por Que Separado

Fulfillment responde:

> De onde o item pode ser atendido?

É responsável por:

- Stores
- Branches
- Warehouses
- Distribution Centers
- Fulfillment Nodes
- Capabilities
- Region / Service Area information

Motivo:

Uma Branch, Store ou Distribution Center existe independentemente do estoque atual.

---

# 15. Freight - Por Que Separado

Freight responde:

> Como o item pode ser entregue, por qual custo e SLA?

É responsável por:

- Freight Quote
- Carrier
- Delivery Method
- Freight Price
- SLA
- Delivery Promise

Motivo:

Freight depende de origem, destino, regras logísticas e integrações externas com Carriers.

---

# 16. Por Que Product, Offer, Price e Availability Não São Um Único Objeto

Um erro comum seria criar:

```text
Product
├─ Seller
├─ Price
├─ Stock
├─ Freight
└─ Branches
```

Isso cria um Aggregate gigante com lifecycles diferentes e acoplamento excessivo.

Em vez disso:

```text
Catalog      → Product/SKU
Sellers      → Seller
Offers       → Seller + SKU
Pricing      → Offer Price
Availability → Sellable State
Fulfillment  → Logistics Nodes
Freight      → Delivery
```

Assim as boundaries permanecem claras e escaláveis.

---

# 17. Modular Monolith First - Por Quê

A plataforma começa como Modular Monolith, e não como dezenas de microservices.

Motivos:

- deployment mais simples
- debugging mais simples
- menor custo operacional
- primeiro definimos boundaries fortes
- evolução mais segura

A arquitetura já fica preparada para service extraction no futuro.

Regra:

> Boundary first, distribution later.

---

# 18. Quando Eu Extrairia um Microservice

Um módulo vira serviço independente quando existe um motivo real:

- independent scaling
- independent deployment
- fault isolation
- different resource profile
- different team ownership
- different security requirements

Possíveis candidatos futuros:

- Availability
- Search
- AI Workers

---

# 19. Database per Bounded Context - Por Quê

Cada Bounded Context é dono de seus dados canônicos.

Exemplo:

```text
Catalog       → Catalog Data
Pricing       → Pricing Data
Availability  → Availability Data
```

Isso evita hidden coupling.

Importante:

Database-per-context significa ownership lógico.

Não significa necessariamente um servidor físico separado para cada contexto no primeiro dia.

---

# 20. Por Que Não Fazer Cross-Context Database Joins

Proibido:

```text
Pricing faz JOIN direto em tabelas de Catalog
```

Motivo:

Isso quebra as boundaries e dificulta a evolução independente dos contexts.

Em vez disso usamos:

- Integration Events
- APIs
- Projections
- Explicit Contracts

---

# 21. Polyglot Persistence - Por Quê

Diferentes tipos de dados pedem diferentes tecnologias.

Direção inicial:

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

Motivo:

Escolher storage conforme Domain e access patterns, não por moda ou preferência pessoal.

---

# 22. Por Que MongoDB para Catalog

Catalog possui:

- flexible attributes
- category-specific specifications
- nested data
- evolving schema

Document storage combina bem com esse modelo.

---

# 23. Por Que Relational DB para Pricing

Pricing exige:

- financial precision
- consistency
- validity periods
- constraints
- history
- auditing
- concurrency

Relational storage é uma escolha forte para esse cenário.

---

# 24. Por Que MongoDB para Availability

Availability pode ter:

- high cardinality
- frequent updates
- node-specific state
- regional projections
- event-driven ingestion

MongoDB é um forte candidato inicial.

---

# 25. Redis - Por Quê

Redis é usado para:

- distributed cache
- low-latency Availability projections
- short-lived Freight cache
- rate limiting
- selected technical state

Regra principal:

> Redis deixa a plataforma mais rápida, mas não vira a autoridade dos dados.

Canonical data continua no Bounded Context dono.

---

# 26. Elasticsearch - Por Quê

Elasticsearch é usado para Search projections.

Ele oferece:

- full-text search
- filtering
- facets
- sorting
- denormalized commerce views
- vector search
- semantic search

Regra principal:

> Elasticsearch é projection, não source of truth.

---

# 27. Por Que Search Usa Projections

Uma customer search page pode precisar de:

```text
Product
Seller
Offer
Price
Availability
```

Em vez de consultar todos os databases de forma síncrona, Kafka events alimentam um Search document denormalizado.

Isso melhora:

- latency
- scalability
- resilience

---

# 28. Kafka - Por Quê

Kafka é usado como asynchronous event backbone.

Motivos:

- high throughput
- durable streams
- replay
- consumer groups
- partition ordering
- vários consumers independentes

Exemplo:

```text
AvailabilityChanged
        ↓
Kafka
    ├─ Redis
    └─ Elasticsearch
```

---

# 29. Por Que Não Usar REST para Tudo

REST é útil quando precisamos de resposta imediata.

Kafka é melhor quando:

- vários consumers precisam reagir ao mesmo evento
- producer não deve depender dos consumers
- eventual consistency é aceitável
- high throughput é necessário

Os dois coexistem.

---

# 30. Domain Events vs Integration Events

Domain Event:

> Algo importante aconteceu dentro do Domain.

Integration Event:

> Um business fact foi publicado para fora do Bounded Context.

Exemplo:

```text
PriceChangedDomainEvent
        ↓
Application mapping
        ↓
PriceChanged
        ↓
Kafka
```

São conceitos diferentes.

---

# 31. Transactional Outbox - Por Quê

Problema:

```text
Save DB
then
Publish Kafka
```

Se o database commit funcionar e Kafka falhar, os dados e eventos ficam inconsistentes.

Solução:

```text
Same transaction:
├─ Save business state
└─ Save Outbox Message
```

Depois um Worker publica o Outbox no Kafka.

Princípio:

> Commit business truth e intent to communicate na mesma transaction local.

---

# 32. Por Que Consumers Precisam Ser Idempotent

Kafka é tratado como at-least-once delivery.

O mesmo event pode chegar mais de uma vez.

Portanto:

```text
same event processado duas vezes
≠
business operation executada incorretamente duas vezes
```

Podemos usar:

- EventId
- Inbox
- SourceVersion
- IdempotencyKey

---

# 33. Inbox - Por Quê

Outbox protege o producer.

Inbox protege o consumer.

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

Isso ajuda a manter processamento assíncrono confiável.

---

# 34. Elasticsearch Rebuild - Por Quê

Search data é derivado.

Então o sistema deve conseguir reconstruir:

```text
Canonical Data / Kafka
        ↓
Search Projection Worker
        ↓
New Elasticsearch Index
```

Usamos versioned indexes e aliases.

Exemplo:

```text
products-v1
products-v2
products-current → alias
```

---

# 35. GenAI - Por Quê

GenAI é usado para reduzir trabalho manual no Catalog.

Primeiro use case:

> Criar ou enriquecer Product/SKU a partir de natural language ou supplier data.

AI pode ajudar com:

- title generation
- description
- category suggestion
- attributes
- specifications
- normalization
- embeddings

---

# 36. GenAI Golden Rule

> AI propõe. O Domain decide.

AI output nunca é authoritative por si só.

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

# 37. Por Que AI Não Escreve Diretamente no MongoDB

Porque AI é probabilistic.

Se AI escrever direto no database:

- Domain rules são ignoradas
- invalid data pode virar canonical
- auditability fica fraca
- provider behavior passa a controlar business state

Por isso AI sempre passa por Application + Domain.

---

# 38. Azure AI e Google AI - Por Que Ambos

A arquitetura usa provider abstraction.

```text
IGenerativeAiProvider
        ↓
   ┌────┴────┐
   ↓         ↓
 Azure     Google
```

Benefícios:

- evitar vendor lock-in
- comparar qualidade
- comparar custo
- escolher model por use case
- fallback quando fizer sentido
- future provider replacement

---

# 39. Structured Output - Por Quê

Para automação, AI deve retornar structured data.

Preferir:

```json
{
  "name": "...",
  "brand": "...",
  "category": "...",
  "attributes": []
}
```

em vez de um texto livre.

Benefícios:

- validation mais simples
- automation mais segura
- testing mais fácil
- menor ambiguity

---

# 40. AI Output É Untrusted

Mesmo JSON válido pode conter informações inventadas.

Por isso validamos:

- schema
- required fields
- allowed values
- Domain invariants
- identifiers
- evidence
- confidence/review policy

---

# 41. Embeddings - Por Quê

Embeddings permitem semantic search.

Exemplo:

```text
"notebook leve para viajar e programar"
```

pode encontrar Products relevantes mesmo sem usar as mesmas keywords.

Flow:

```text
Product text
    ↓
Embedding Model
    ↓
Vector
    ↓
Elasticsearch
```

---

# 42. RAG - Por Quê

RAG fornece ao LLM contexto relevante e confiável antes da geração.

Flow:

```text
Question
  ↓
Retrieve relevant information
  ↓
Prompt + Context
  ↓
LLM
```

Use cases:

- Product knowledge
- Documentation
- Catalog support
- future Commerce Assistant

RAG melhora grounding, mas não substitui Domain validation.

---

# 43. Tool Calling - Por Quê

Future AI assistants podem chamar Application capabilities aprovadas.

Exemplos de tools:

```text
SearchCatalog
GetProduct
GetPrice
GetAvailability
GetFreight
```

O LLM não recebe acesso direto ao database.

---

# 44. Por Que Não Agent-First

A primeira implementação usa deterministic workflows com chamadas de AI.

Motivo:

Agents adicionam:

- autonomy
- complexity
- debugging difficulty
- cost uncertainty

Começamos com:

```text
Workflow
→ AI
→ Validation
→ Domain
```

Agents entram apenas quando trouxerem valor claro.

---

# 45. AI Observability - Por Quê

AI possui variação de:

- cost
- latency
- quality
- token usage

Precisamos rastrear:

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

Assim AI deixa de ser caixa-preta e vira engenharia mensurável.

---

# 46. Prompt Versioning - Por Quê

Alterar um prompt pode mudar o comportamento sem mudar o C# code.

Por isso prompts são versioned assets.

Exemplo:

```text
product-extraction:v1
product-extraction:v2
```

Isso permite regression testing e auditability.

---

# 47. Azure - Por Que Primary Cloud

Azure é o primary cloud inicial por causa de:

- forte integração com .NET
- AKS
- Managed Identity
- Key Vault
- Entra ID
- Azure Monitor
- Application Insights
- Container Registry
- forte adoção enterprise

Mas o core continua provider-independent.

---

# 48. Azure-First, Not Azure-Locked

Princípio:

> Azure hospeda Yunu.Commerce. Azure não define Yunu.Commerce.

Cloud SDKs ficam em Infrastructure.

Domain permanece independente.

---

# 49. AKS - Por Quê

AKS oferece:

- container orchestration
- scaling
- health probes
- rolling deployment
- workload isolation
- future service extraction

Isso não significa começar com dezenas de microservices.

---

# 50. Key Vault - Por Quê

Usado para secrets:

- DB credentials
- API keys
- external provider credentials
- certificates

Nunca commitamos secrets no Git.

---

# 51. Managed Identity - Por Quê

Managed Identity evita static credentials ao acessar Azure services.

Benefícios:

- menos secrets
- credential lifecycle automático
- melhor segurança
- RBAC integration

---

# 52. Entra ID - Por Quê

Usado para identity/authentication scenarios.

Suporta:

- OAuth
- OIDC
- JWT
- enterprise identity
- service identities

Authorization continua na Application layer.

---

# 53. OpenTelemetry - Por Quê

Observability não deve ficar presa a um vendor.

OpenTelemetry fornece:

- traces
- metrics
- logs

Depois enviamos para:

```text
Application Insights
Azure Monitor
outros backends compatíveis
```

---

# 54. Docker - Por Quê

Docker fornece o mesmo packaging model para:

- developer machine
- CI
- AKS

Isso melhora a consistência entre environments.

---

# 55. Testcontainers - Por Quê

Integration tests podem usar Infrastructure real:

- MongoDB
- SQL
- Kafka
- Redis
- Elasticsearch

Benefícios:

- realistic tests
- repeatability
- menor dependência da máquina do developer

---

# 56. Architecture Tests - Por Quê

Documentation sozinha não garante arquitetura.

Architecture tests podem quebrar o build se:

```text
Domain referencia Infrastructure
Application referencia MongoDB.Driver
AI Application referencia Azure SDK
Domain referencia Domain de outro Bounded Context
```

Isso transforma architecture rules em regras executáveis.

---

# 57. First Vertical Slice

O primeiro end-to-end business slice deve ser Catalog.

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

Esse flow prova quase a arquitetura inteira usando um único use case.

---

# 58. Por Que Começar com Catalog

Catalog é o melhor first slice porque conecta:

- DDD
- MongoDB
- GenAI
- Outbox
- Kafka
- Elasticsearch
- embeddings depois
- API
- testing
- observability

Ele vira a reference implementation arquitetural para os outros contexts.

---

# 59. Pergunta de Entrevista: Explique a Arquitetura

### Pergunta

**Can you explain the architecture of your GenAI commerce project?**

### Resposta

I designed it using DDD, Clean Architecture, Hexagonal Architecture and Event-Driven Architecture.

DDD separates the platform into Bounded Contexts such as Catalog, Sellers, Offers, Pricing, Availability, Fulfillment and Freight.

Clean Architecture keeps business rules independent from infrastructure.

Hexagonal Architecture isolates technologies such as MongoDB, Kafka, Redis, Elasticsearch and AI providers behind Ports and Adapters.

Kafka is used for asynchronous integration between contexts, while the Transactional Outbox guarantees reliable event publication.

Search is implemented as an Elasticsearch projection, Redis is used for low-latency cache/projections, and Generative AI is exposed through provider-neutral abstractions so I can use Azure AI or Google AI without changing the business core.

---

# 60. Pergunta de Entrevista: Why DDD?

### Pergunta

**Why did you choose DDD?**

### Resposta

Because the platform has multiple complex business areas with different rules and lifecycles.

I did not want Product, Seller, Price, Availability and Freight mixed into one giant model.

DDD gives each capability clear ownership and a Ubiquitous Language.

---

# 61. Pergunta de Entrevista: Why Clean Architecture?

### Pergunta

**Why Clean Architecture?**

### Resposta

To protect the Domain from technical decisions.

For example, Catalog should not care whether I persist Product in MongoDB or another database.

The same applies to Kafka, Redis, Elasticsearch and AI providers.

Infrastructure depends on the core, not the opposite.

---

# 62. Pergunta de Entrevista: Why Hexagonal Architecture?

### Pergunta

**What does Hexagonal Architecture give you here?**

### Resposta

It gives me Ports and Adapters.

For example, the Application can depend on IGenerativeAiProvider while Azure AI and Google AI are separate adapters.

The same pattern applies to databases, Kafka, Search and carrier APIs.

This makes provider replacement much safer.

---

# 63. Pergunta de Entrevista: Why Kafka?

### Pergunta

**Why Kafka instead of calling services directly?**

### Resposta

I still use synchronous APIs when an immediate response is required.

Kafka is used when contexts need to react independently.

For example, when a Product changes, Search and AI embedding workers can consume the same event independently.

This reduces runtime coupling and also gives me replay and horizontal consumer scaling.

---

# 64. Pergunta de Entrevista: Why Outbox?

### Pergunta

**Why use Transactional Outbox?**

### Resposta

Because saving business state and publishing Kafka are two independent operations.

Without Outbox, the database can commit and Kafka can fail.

With Outbox, the Aggregate change and the Integration Event intent are committed in the same local transaction, and a Worker publishes later.

---

# 65. Pergunta de Entrevista: Why Redis?

### Pergunta

**Why Redis?**

### Resposta

For low-latency distributed reads and derived projections, especially Availability.

Redis is never my default source of truth.

If Redis disappears, canonical commerce data still exists and projections can be rebuilt.

---

# 66. Pergunta de Entrevista: Why Elasticsearch?

### Pergunta

**Why Elasticsearch?**

### Resposta

Because product discovery needs full-text search, filters, facets and denormalized commerce views.

It also gives me a path to vector and hybrid semantic search.

But Elasticsearch is a projection. Catalog, Pricing and Availability remain authoritative.

---

# 67. Pergunta de Entrevista: Why Different Databases?

### Pergunta

**Why not use SQL for everything?**

### Resposta

Because different contexts have different data characteristics.

Catalog has flexible attributes and document-oriented structures, so MongoDB is a good fit.

Pricing benefits from relational precision and constraints.

Availability has high-frequency state updates.

I choose the persistence technology based on Domain and access patterns.

---

# 68. Pergunta de Entrevista: Why Modular Monolith?

### Pergunta

**Why not start with microservices?**

### Resposta

Because distribution has a cost.

I prefer to establish strong Bounded Context and project boundaries first inside a Modular Monolith.

If Availability or Search later needs independent scaling or deployment, I can extract it without redesigning the Domain.

---

# 69. Pergunta de Entrevista: How Does GenAI Fit?

### Pergunta

**How does Generative AI fit into the architecture?**

### Resposta

AI is an external reasoning capability, not the owner of business truth.

For Product registration, AI converts unstructured input into a structured proposal.

That proposal goes through Application validation and Catalog Domain rules before it becomes canonical data.

So the AI helps automate the workflow without bypassing DDD.

---

# 70. Pergunta de Entrevista: Azure vs Google AI

### Pergunta

**Why support Azure AI and Google AI?**

### Resposta

I do not want the Application architecture coupled to one AI vendor.

I define provider-neutral Ports and implement Azure and Google adapters.

Then I can select based on model quality, cost, latency or capability without changing Catalog Domain.

---

# 71. Pergunta de Entrevista: What Is the Source of Truth?

### Pergunta

**What is your source-of-truth strategy?**

### Resposta

Each Bounded Context owns its canonical data.

Redis and Elasticsearch are derived data.

Kafka carries Integration Events.

AI stores proposals and execution metadata but does not own Product or Price truth.

---

# 72. Pergunta de Entrevista: How Do Contexts Communicate?

### Pergunta

**How do Bounded Contexts communicate?**

### Resposta

Through explicit contracts.

For synchronous needs, APIs or Application contracts.

For asynchronous propagation, versioned Integration Events through Kafka.

They never integrate by directly reading another context's database.

---

# 73. Pergunta de Entrevista: Eventual Consistency

### Pergunta

**How do you handle consistency across contexts?**

### Resposta

Strong consistency stays inside the Aggregate and owning context.

Across contexts I use eventual consistency with reliable events.

For example, Pricing commits the Price, publishes PriceChanged through Outbox/Kafka, and Search updates its projection asynchronously.

---

# 74. Pergunta de Entrevista: How Do You Handle Duplicates?

### Pergunta

**Kafka can deliver messages more than once. What do you do?**

### Resposta

Consumers are idempotent.

I use EventId, Inbox or source versions depending on the use case.

I design around at-least-once delivery rather than assuming perfect exactly-once processing.

---

# 75. Pergunta de Entrevista: How Do You Handle Search Rebuild?

### Pergunta

**What happens if Elasticsearch is lost?**

### Resposta

Nothing canonical is lost because Elasticsearch is derived.

I can create a new versioned index, rebuild it from canonical data and/or Kafka events, validate it and then switch the alias.

---

# 76. Pergunta de Entrevista: Why Architecture Tests?

### Pergunta

**How do you prevent developers from breaking the architecture?**

### Resposta

I do not rely only on documentation.

I enforce project references and architecture tests.

For example, a test can fail if Domain references MongoDB.Driver or if one Bounded Context references another context's Domain assembly.

---

# 77. Pergunta de Entrevista: How Would You Scale Availability?

### Pergunta

**Availability becomes very high volume. What would you do?**

### Resposta

Availability is already isolated as its own context.

I can independently scale its consumers and read projections.

Kafka handles the event stream, MongoDB stores canonical Availability state, and Redis provides low-latency reads.

If necessary, Availability can later be extracted into its own service.

---

# 78. Pergunta de Entrevista: What About Cloud Lock-In?

### Pergunta

**Are you locked into Azure?**

### Resposta

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
→ O que é o Product?

Sellers
→ Quem está vendendo?

Offers
→ Qual relação comercial existe entre Seller e SKU?

Pricing
→ Quanto custa?

Availability
→ Pode ser atendido?

Fulfillment
→ De onde pode ser atendido?

Freight
→ Como pode ser entregue?
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

# 82. A Mensagem Mais Importante Para Entrevista

A ideia arquitetural mais forte é:

> Eu não estou usando tecnologias porque estão na moda. Cada technology possui uma responsabilidade e uma boundary específica.

Exemplos:

```text
MongoDB
porque Catalog data é flexível.

SQL
porque Pricing precisa de financial consistency.

Kafka
porque contexts precisam de decoupled asynchronous integration.

Redis
porque Availability precisa de very low read latency.

Elasticsearch
porque customer discovery precisa de um denormalized Search model.

GenAI
porque pode automatizar Catalog onboarding, mas permanece fora do Domain.

Azure
porque é o primary cloud inicial, enquanto business logic continua cloud-independent.
```

---

# 83. Resumo Final

Yunu.Commerce foi desenhado para crescer em complexidade sem virar um sistema fortemente acoplado.

A arquitetura separa deliberadamente:

```text
business logic
application orchestration
infrastructure
integration
search
AI
cloud deployment
```

O princípio final de engenharia é:

> Domains own business truth. Application orchestrates. Infrastructure adapts. Events integrate. Search projects. AI assists. Cloud hosts.
