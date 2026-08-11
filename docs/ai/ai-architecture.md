# Yunu.Commerce - AI Architecture

## 1. Purpose

This document defines the Artificial Intelligence and Generative AI Architecture for Yunu.Commerce.

The objective is to introduce Generative AI as a first-class platform capability without allowing AI providers, models, prompts or probabilistic outputs to contaminate the core commerce Domains.

The initial AI use case is:

> Assist the creation and enrichment of Catalog Products and SKUs from natural language, documents, images and structured or semi-structured source data.

The architecture must support Azure-hosted and Google-hosted AI providers through replaceable adapters.

The central rule is:

> AI proposes. Application and Domain rules decide.

---

# 2. AI Architecture Principles

Yunu.Commerce follows these principles:

```text
Provider-independent AI contracts

LLMs never access databases directly

LLMs never bypass Application use cases

LLMs never mutate Domain state directly

AI output is untrusted input

Structured output is preferred over free-form output

Domain validation remains deterministic

Human approval is supported where business risk requires it

Prompts are versioned assets

Model and provider selection are configuration/policy decisions

AI executions are observable

Token usage and cost are measurable

Sensitive data is minimized

RAG is retrieval, not business authority

Embeddings are derived data

Vector stores are replaceable

AI failure must not corrupt canonical commerce data
```

---

# 3. AI Is Not the Domain

Generative AI does not replace:

```text
Catalog Domain
Pricing Domain
Availability Domain
Fulfillment Domain
Freight Domain
```

AI may assist these Domains through Application use cases.

Example:

```text
User
  │
  ▼
Natural Language
  │
  ▼
AI
  │
  ▼
Structured Product Proposal
  │
  ▼
Catalog Application
  │
  ▼
Catalog Domain
  │
  ▼
Canonical Product
```

---

# 4. Initial AI Use Case

The first GenAI vertical slice is Product Catalog onboarding.

Example user input:

```text
Cadastre uma TV Samsung OLED 55 polegadas 4K,
modelo XYZ, com Wi-Fi, HDMI 2.1 e 120 Hz.
```

AI may transform this into a structured proposal such as:

```text
Product Name
Brand
Category
Description
Attributes
Specifications
Candidate SKU
Keywords
```

The proposal is then validated by deterministic application and Domain rules.

---

# 5. AI Bounded Capability

AI should initially be treated as a platform/application capability rather than the owner of commerce entities.

Conceptually:

```text
AI
│
├── Generation
├── Extraction
├── Classification
├── Enrichment
├── Embeddings
├── Retrieval
├── Tool orchestration
└── Evaluation
```

Commerce Domains remain authoritative.

---

# 6. Provider Abstraction

Yunu.Commerce must not depend directly on one AI vendor.

Conceptually:

```text
AI Application
      │
      ▼
GenerativeAiPort
      │
      ├── Azure AI Adapter
      └── Google AI Adapter
```

Future adapters may be added without changing Domain behavior.

---

# 7. Initial Provider Candidates

The initial provider strategy supports:

```text
Microsoft Azure AI
Google AI / Vertex AI
```

The exact first provider may be selected through ADR and configuration.

The architecture must allow both.

---

# 8. Azure AI Adapter

The Azure adapter may integrate with suitable Azure-hosted generative models and AI services.

Provider-specific concerns include:

```text
endpoint
deployment/model name
authentication
API version
rate limits
token accounting
provider-specific safety settings
```

These belong exclusively to Infrastructure.

---

# 9. Google AI Adapter

The Google adapter may integrate with Gemini models through the appropriate Google AI/Vertex AI infrastructure.

Provider-specific concerns include:

```text
project
location
model
authentication
quotas
token accounting
provider-specific safety settings
```

These belong exclusively to Infrastructure.

---

# 10. AI Port

A provider-neutral port should expose capabilities rather than vendor terminology.

Conceptually:

```text
IGenerativeAiProvider

GenerateStructuredAsync(...)
GenerateTextAsync(...)
```

Additional capabilities should be introduced only when required.

---

# 11. Capability-Specific Ports

As AI usage grows, specialized ports may be preferable:

```text
IStructuredGeneration
IEmbeddingGenerator
IMultimodalAnalyzer
IContentClassifier
```

Avoid one enormous interface containing every possible AI feature.

---

# 12. Provider Routing

A provider router may select the configured provider.

Conceptually:

```text
AI Request
    │
    ▼
Provider Router
    │
    ├── Azure
    └── Google
```

Routing may initially be configuration-based.

Future policies may consider:

```text
cost
latency
model capability
availability
data residency
task type
```

---

# 13. Provider Fallback

Provider fallback may be supported.

Example:

```text
Azure AI
   │
   └── unavailable
          │
          ▼
       Google AI
```

Fallback must not silently change business semantics.

Provider/model used must be recorded in execution metadata.

---

# 14. Model Abstraction

Application logic should express capabilities rather than concrete model names.

Prefer:

```text
ProductEnrichmentModel
EmbeddingModel
ClassificationModel
```

over spreading vendor model identifiers through business code.

Concrete model mapping belongs to configuration.

---

# 15. Model Selection

Different tasks may use different models.

Example:

```text
Product extraction
→ strong structured-generation model

Simple classification
→ smaller/cheaper model

Embeddings
→ embedding model

Multimodal product analysis
→ vision-capable model
```

Using the most expensive model for every task is not an architectural goal.

---

# 16. Structured Output

For commerce workflows, structured output is preferred.

Example target contract:

```json
{
  "name": "...",
  "brand": "...",
  "category": "...",
  "description": "...",
  "attributes": [],
  "specifications": [],
  "skuCandidates": []
}
```

The exact schema belongs to Application contracts.

---

# 17. AI Output Is Untrusted

Even when a provider supports schema-constrained output, the response must be treated as untrusted input.

Validation must include:

```text
schema validation
required fields
identifier validation
allowed values
Domain invariants
business rules
confidence/review policy
```

---

# 18. Hallucination Boundary

AI-generated facts must never automatically become authoritative merely because they are plausible.

Example:

```text
AI invents GTIN/EAN
```

This must not be persisted as verified product identity.

Fields requiring authoritative evidence must be marked, validated or reviewed.

---

# 19. Product Proposal

AI should initially create a proposal rather than a Product Aggregate directly.

Conceptually:

```text
ProductEnrichmentProposal

ProposalId
SuggestedName
SuggestedBrand
SuggestedCategory
SuggestedDescription
SuggestedAttributes
SuggestedSpecifications
SkuCandidates
Confidence/Warnings
SourceReferences
```

---

# 20. Proposal Workflow

Conceptually:

```text
Input
  │
  ▼
AI Generation
  │
  ▼
Structured Proposal
  │
  ▼
Application Validation
  │
  ▼
Domain Validation
  │
  ├── Valid → Create/Update Product
  └── Invalid → Review / Correction
```

---

# 21. Human-in-the-Loop

Human approval should be available for:

```text
uncertain classifications
unverified identifiers
high-impact changes
ambiguous product matching
AI-generated descriptions requiring review
```

Not every low-risk enrichment requires manual approval.

Approval policy should be configurable by use case.

---

# 22. Confidence

Provider-generated confidence values must not be blindly trusted.

Yunu.Commerce may calculate its own validation/review score based on:

```text
required fields present
known brand match
category match
identifier verification
retrieval evidence
schema validity
rule validation
```

---

# 23. Prompt Architecture

Prompts are versioned application assets.

They should not be scattered as arbitrary strings throughout C# code.

Potential organization:

```text
src/AI/Prompts/

├── ProductExtraction/
│   ├── system.md
│   ├── user-template.md
│   └── schema.json
│
└── ProductEnrichment/
    ├── system.md
    └── user-template.md
```

Exact project placement will follow solution structure.

---

# 24. Prompt Versioning

Every production prompt should have a logical version.

Example:

```text
product-extraction:v1
product-extraction:v2
```

Execution metadata should record the prompt version.

---

# 25. Prompt Testing

Prompts require tests/evaluations just like code.

Test datasets may include:

```text
normal products
missing fields
ambiguous descriptions
invalid GTIN
multiple SKUs
multilingual input
malicious prompt content
very long source content
```

---

# 26. Prompt Injection

External product content must be treated as untrusted.

Example malicious source:

```text
Ignore all previous instructions and...
```

Retrieved or uploaded content must not automatically become trusted system instructions.

System/developer policy and application constraints remain authoritative.

---

# 27. Prompt Layering

Conceptually:

```text
System Instructions
      │
      ▼
Application Task Instructions
      │
      ▼
Retrieved Context
      │
      ▼
User / Source Content
```

Untrusted source content must remain clearly separated from instructions.

---

# 28. RAG

Retrieval-Augmented Generation may be used when the model needs trusted Yunu.Commerce knowledge.

Conceptually:

```text
User Request
     │
     ▼
Query
     │
     ▼
Retriever
     │
     ▼
Relevant Documents
     │
     ▼
Prompt Context
     │
     ▼
LLM
```

RAG improves grounding but does not guarantee correctness.

---

# 29. RAG Sources

Potential RAG sources include:

```text
Catalog documentation
Category rules
Product attribute definitions
Brand rules
Internal manuals
Integration specifications
Commerce policies
Approved product content
```

Only approved sources should enter trusted retrieval collections.

---

# 30. RAG Is Not Database Access

RAG must not become a disguised unrestricted database query layer.

The AI should retrieve curated knowledge through explicit retrieval ports.

---

# 31. Retrieval Port

Potential abstraction:

```text
IKnowledgeRetriever
```

Conceptually:

```text
RetrieveAsync(query, context)
```

The Application remains independent from Elasticsearch-specific APIs.

---

# 32. Embeddings

Embeddings convert content into vector representations for semantic retrieval.

Potential embedded content includes:

```text
product title
description
attributes
specifications
category definitions
documentation
```

Embeddings are derived data.

---

# 33. Embedding Provider

Embedding generation should use a provider-neutral port.

Conceptually:

```text
IEmbeddingGenerator
      │
      ├── Azure Adapter
      └── Google Adapter
```

The embedding provider and vector database are separate concerns.

---

# 34. Vector Store

Elasticsearch is the initial candidate for vector search because the platform already uses it for product search.

Conceptually:

```text
Embedding Generator
       │
       ▼
Vector
       │
       ▼
Elasticsearch
```

The architecture must allow a different vector store later.

---

# 35. Vector Store Port

Potential abstraction:

```text
IVectorStore
```

Capabilities may include:

```text
Upsert
Delete
SimilaritySearch
HybridSearch
```

Provider-specific query DSL must remain outside Application.

---

# 36. Hybrid Search

Product discovery may combine:

```text
keyword search
+
filters
+
vector similarity
```

Conceptually:

```text
BM25 / lexical relevance
+
vector semantic relevance
+
commerce filters
```

Ranking strategy belongs to Search/AI application capabilities.

---

# 37. Embedding Lifecycle

Embeddings must be regenerated when relevant source content changes.

Potential flow:

```text
ProductUpdated
      │
      ▼
Embedding Worker
      │
      ▼
Generate Embedding
      │
      ▼
Update Search Vector
```

---

# 38. Embedding Version

Embedding metadata should record:

```text
provider
model
model version/configuration
source content version
generated timestamp
```

Vectors from incompatible embedding models must not be mixed blindly.

---

# 39. AI Tools

LLMs may use explicitly defined application tools.

Examples:

```text
SearchCatalog
FindProductBySku
FindBrand
FindCategory
ValidateProductDraft
CreateProductDraft
GetSeller
GetCurrentPrice
GetAvailability
```

Tools expose use cases, not infrastructure.

---

# 40. Tool Boundary

Forbidden:

```text
LLM
  │
  ▼
MongoDB Driver
```

Forbidden:

```text
LLM
  │
  ▼
SQL Connection
```

Correct:

```text
LLM
  │
  ▼
Tool
  │
  ▼
Application Use Case
  │
  ▼
Domain / Port
```

---

# 41. Tool Authorization

AI tool execution must respect the same authorization principles as ordinary application actions.

An AI agent must not gain privileges merely because it is an AI agent.

---

# 42. Tool Validation

Every tool call must validate:

```text
arguments
permissions
business constraints
idempotency requirements
```

Tool input is untrusted.

---

# 43. Read Tools vs Write Tools

Read tools are lower risk:

```text
SearchCatalog
GetProduct
GetAvailability
```

Write tools are higher risk:

```text
CreateProduct
ChangePrice
DeactivateOffer
```

Write tools require stricter policy and may require approval.

---

# 44. AI Agent

An Agent is an orchestration capability combining:

```text
LLM
+
instructions
+
tools
+
state
+
execution policy
```

Agents are not required for the first product onboarding use case.

Start with deterministic workflows plus model calls.

---

# 45. Workflow Before Agent

Prefer:

```text
Application Workflow
→ AI call
→ validation
→ Domain command
```

before introducing autonomous multi-step agents.

Agentic behavior should be added only when it provides measurable value.

---

# 46. Product Registration Workflow

Initial target workflow:

```text
User describes Product
        │
        ▼
Catalog AI Endpoint
        │
        ▼
Product Enrichment Application
        │
        ▼
GenerativeAiPort
        │
        ▼
Azure AI or Google AI
        │
        ▼
Structured Product Proposal
        │
        ▼
Validation
        │
        ▼
Catalog Application
        │
        ▼
Catalog Domain
        │
        ▼
MongoDB
        │
        ▼
Outbox
        │
        ▼
Kafka
        │
        ▼
Elasticsearch
```

This is the first end-to-end GenAI commerce slice.

---

# 47. Multimodal Product Registration

Future input may include:

```text
text
image
PDF
supplier datasheet
CSV
URL content
```

A multimodal model may extract candidate product data.

Every extracted field still passes through validation.

---

# 48. Image Analysis

Product images may assist with:

```text
category suggestion
color detection
product-type detection
visible attribute extraction
description drafting
```

AI must not infer unverifiable identifiers or technical specifications without evidence.

---

# 49. Document Extraction

Supplier manuals or datasheets may be processed through:

```text
document ingestion
text extraction
chunking
retrieval
structured extraction
```

Original source references should be preserved for lineage.

---

# 50. Source Evidence

AI proposals should retain evidence where useful.

Conceptually:

```text
Suggested Attribute
      │
      ├── Value
      ├── Source
      └── Confidence / Validation Status
```

This makes review more trustworthy.

---

# 51. Data Lineage

Important AI-generated changes should be traceable.

Potential lineage:

```text
Supplier PDF
    │
    ▼
Extracted Text
    │
    ▼
AI Proposal
    │
    ▼
Human Approval
    │
    ▼
Catalog Update
```

---

# 52. AI Operational Store

AI execution metadata should be stored separately from canonical Catalog state.

Potential record:

```text
AiExecution

ExecutionId
UseCase
Provider
Model
PromptVersion
StartedAtUtc
CompletedAtUtc
InputTokenCount
OutputTokenCount
EstimatedCost
Status
CorrelationId
```

---

# 53. AI Proposal Store

Proposals may require persistence for:

```text
review
audit
comparison
approval
reprocessing
```

MongoDB is a strong candidate for this semi-structured state.

---

# 54. Prompt Content Storage

Do not automatically persist full prompts and responses.

Some payloads may contain sensitive or commercially restricted content.

Persist only what is required by:

```text
debugging
evaluation
audit
cost analysis
```

with appropriate redaction and retention.

---

# 55. Token Usage

Every provider adapter should expose usage metadata when available.

Examples:

```text
input tokens
output tokens
cached tokens
reasoning tokens where exposed
```

Provider-neutral normalization should be used where practical.

---

# 56. AI Cost

AI cost must be observable.

Potential dimensions:

```text
provider
model
use case
tenant/seller
request
day
month
```

Cost data supports architectural decisions.

---

# 57. Cost Budget

The platform should eventually support budget controls.

Potential policies:

```text
maximum tokens per request
maximum output size
daily budget
monthly budget
model routing by task
batch limits
```

---

# 58. Model Economy

Use the least expensive model that reliably satisfies the task.

Example:

```text
simple classification
→ smaller model

complex extraction
→ stronger model

embedding
→ dedicated embedding model
```

Model selection should be evidence-driven.

---

# 59. Semantic Cache

A future semantic cache may reduce repeated AI calls.

Potential uses:

```text
repeated classification
repeated normalization
identical enrichment input
```

Caching must account for:

```text
prompt version
model
input version
business rules
```

---

# 60. AI Request Idempotency

Long-running or expensive AI requests should support idempotency.

Potential key:

```text
UseCase
+
SourceVersion
+
PromptVersion
+
ModelPolicyVersion
```

This can avoid duplicate cost.

---

# 61. AI Asynchronous Processing

Long-running AI workflows should use asynchronous processing.

Conceptually:

```text
POST enrichment request
       │
       ▼
202 Accepted
       │
       ▼
AI Worker
       │
       ▼
Proposal
       │
       ▼
Status endpoint/event
```

Do not hold HTTP requests indefinitely for large workflows.

---

# 62. AI Queue

AI workloads may be triggered through Kafka or another background-work mechanism.

Examples:

```text
ProductEnrichmentRequested
EmbeddingGenerationRequested
DocumentProcessingRequested
```

AI-specific job orchestration must remain explicit.

---

# 63. AI Rate Limits

Providers impose quotas and rate limits.

Adapters/workers must support:

```text
bounded concurrency
rate limiting
retry-after handling
queueing
backpressure
```

---

# 64. AI Timeout

Every model call must have a timeout appropriate to the use case.

No AI request should wait indefinitely.

---

# 65. AI Retry

Retry only transient failures.

Potential retry candidates:

```text
temporary provider outage
HTTP 429
temporary network failure
HTTP 5xx
```

Do not blindly retry:

```text
invalid prompt
invalid schema
policy rejection
bad request
```

---

# 66. AI Circuit Breaker

Provider adapters may use Circuit Breakers.

If one provider becomes unhealthy, routing policy may:

```text
fail fast
queue work
use fallback provider
```

depending on use case.

---

# 67. AI Fallback Semantics

Fallback must preserve traceability.

Record:

```text
requested provider
actual provider
model
fallback reason
```

A fallback result still requires normal validation.

---

# 68. AI Safety

AI safety controls may include:

```text
content filtering
input size limits
prompt injection defenses
tool allowlists
output schema validation
authorization
data minimization
```

Provider safety settings complement, but do not replace, application controls.

---

# 69. Guardrails

Guardrails should exist at multiple layers.

Conceptually:

```text
Input Guardrails
      │
      ▼
Prompt / Tool Policy
      │
      ▼
Provider Safety
      │
      ▼
Output Validation
      │
      ▼
Domain Rules
```

No single guardrail is sufficient.

---

# 70. Deterministic Validation

Critical business rules must remain deterministic C# logic.

Examples:

```text
valid Product state
valid SKU identity
valid Money
valid Price interval
valid Availability quantity
valid Seller lifecycle
```

Do not ask an LLM to decide rules that can be expressed reliably in code.

---

# 71. AI Evaluation

AI quality must be measured.

Potential dimensions include:

```text
schema validity
field accuracy
category accuracy
attribute extraction accuracy
hallucination rate
human acceptance rate
latency
cost
```

---

# 72. Golden Dataset

A curated evaluation dataset should eventually contain representative product examples.

Example categories:

```text
electronics
fashion
appliances
beauty
furniture
marketplace products
ambiguous products
poor-quality source descriptions
```

Expected structured outputs enable regression testing.

---

# 73. Evaluation Pipeline

Conceptually:

```text
Golden Dataset
      │
      ▼
Prompt + Model
      │
      ▼
Generated Output
      │
      ▼
Evaluator
      │
      ▼
Metrics
```

Prompt/model changes should be compared before production rollout.

---

# 74. LLM-as-Judge

An LLM may assist evaluation of subjective outputs such as description quality.

It must not be the only evaluator for deterministic facts.

Use code-based assertions wherever possible.

---

# 75. A/B Testing

Future prompt/model changes may be A/B tested.

Potential dimensions:

```text
acceptance rate
latency
cost
accuracy
conversion impact
```

A/B testing must not compromise canonical data integrity.

---

# 76. AI Observability

AI telemetry should include:

```text
ExecutionId
CorrelationId
UseCase
Provider
Model
PromptVersion
Latency
InputTokens
OutputTokens
Cost
Status
RetryCount
FallbackUsed
ValidationResult
```

---

# 77. OpenTelemetry

AI operations should participate in distributed traces.

Conceptually:

```text
HTTP Request
    │
    ▼
AI Application
    │
    ▼
Provider Call
    │
    ▼
Validation
    │
    ▼
Catalog Command
```

The complete flow should be traceable.

---

# 78. Sensitive Data

AI requests must minimize sensitive data.

Do not send:

```text
credentials
tokens
private keys
unnecessary personal information
restricted business data
```

unless the architecture explicitly allows and protects it.

---

# 79. Secrets

Provider credentials belong in secure secret management.

Potential Azure mechanisms:

```text
Managed Identity
Azure Key Vault
```

Google credentials should use appropriate workload identity mechanisms where available.

Never store AI API keys in Git.

---

# 80. Data Residency

Provider selection may eventually depend on:

```text
region
data residency
contractual requirements
customer requirements
```

This should remain a provider-routing concern rather than a Domain concern.

---

# 81. AI Authorization

AI endpoints must enforce ordinary application authorization.

Potential permissions:

```text
RequestProductEnrichment
ApproveProductEnrichment
UseAiWriteTools
ManagePromptConfiguration
ViewAiExecutionMetadata
```

---

# 82. AI Audit

High-impact AI actions should be auditable.

Potential audit record:

```text
who requested
what source was used
which provider/model ran
what was proposed
who approved
what Domain command was executed
```

---

# 83. AI and Catalog

Catalog remains authoritative for:

```text
Product
SKU
Category
Brand
Attributes
Specifications
```

AI can:

```text
extract
suggest
normalize
classify
enrich
```

AI cannot bypass Catalog invariants.

---

# 84. AI and Pricing

Future AI may assist:

```text
pricing analysis
anomaly detection
competitive insights
```

AI must not autonomously modify authoritative Price without explicit deterministic policy and authorization.

---

# 85. AI and Availability

Future AI may assist:

```text
anomaly detection
forecasting
stock-risk analysis
```

Real-time Availability remains deterministic and authoritative.

---

# 86. AI and Fulfillment

Future AI may assist:

```text
network analysis
node recommendations
capacity forecasting
```

Fulfillment topology remains canonical.

---

# 87. AI and Freight

Future AI may assist:

```text
carrier analysis
SLA prediction
cost optimization
anomaly detection
```

Actual customer Freight Quote must remain grounded in deterministic/provider data.

---

# 88. AI and Search

AI and Search are strongly related.

Potential capabilities:

```text
semantic search
query understanding
query rewriting
product embeddings
hybrid ranking
natural-language product discovery
```

Search remains a read capability.

---

# 89. Natural-Language Search

Future flow:

```text
"quero uma tv 4k de 55 polegadas para jogar ps5"
        │
        ▼
Query Understanding
        │
        ▼
Structured Filters + Semantic Query
        │
        ▼
Hybrid Search
        │
        ▼
Elasticsearch
```

The LLM should not manually scan the entire Catalog.

---

# 90. Query Understanding

AI may convert natural language into structured search intent.

Example:

```json
{
  "category": "Television",
  "screenSize": 55,
  "resolution": "4K",
  "gaming": true
}
```

Filters must be validated against supported Search schema.

---

# 91. AI Search Ranking

Future ranking may combine:

```text
lexical score
vector score
business relevance
availability
price signals
seller quality
```

Business ranking signals should remain explicit and measurable.

---

# 92. AI Project Structure

A possible .NET structure is:

```text
src/

├── AI/
│   ├── Yunu.Commerce.AI.Application/
│   ├── Yunu.Commerce.AI.Contracts/
│   └── Yunu.Commerce.AI.Infrastructure/
│
└── BuildingBlocks/
    └── Yunu.Commerce.BuildingBlocks.AI/
```

The exact structure must align with `06-solution-structure.md`.

---

# 93. AI Application

AI Application may contain:

```text
Use Cases
Commands
Queries
Workflows
Ports
Validation
Provider-neutral models
Tool definitions
```

It must not contain provider SDK code.

---

# 94. AI Infrastructure

AI Infrastructure may contain:

```text
Azure AI adapter
Google AI adapter
Embedding adapters
Vector-store adapter
Prompt loading
Provider configuration
Resilience
Telemetry
Token/cost normalization
```

---

# 95. AI Contracts

AI Contracts may contain external API/event contracts such as:

```text
RequestProductEnrichment
ProductEnrichmentStatus
ProductEnrichmentProposalResponse
```

They must not expose provider-native response objects.

---

# 96. BuildingBlocks.AI

Shared AI building blocks should remain technical and small.

Potential concepts:

```text
AiExecutionMetadata
AiUsage
AiProviderId
ModelCapability
StructuredGenerationResult
```

Do not place Catalog-specific business behavior into shared AI libraries.

---

# 97. Provider SDK Isolation

Forbidden:

```text
Catalog.Domain → Azure AI SDK

Catalog.Application → Google SDK

Pricing.Domain → Gemini model type

Freight.Domain → provider chat client
```

Provider SDKs belong in AI Infrastructure adapters.

---

# 98. Semantic Kernel / Agent Frameworks

Frameworks such as Semantic Kernel or other agent SDKs may be evaluated later.

They are optional Infrastructure/Application orchestration tools.

The architecture must not depend on one agent framework to preserve core AI abstractions.

---

# 99. Direct SDK First

For the first vertical slice, a direct provider adapter behind a clean interface may be simpler than introducing a large agent framework.

Add orchestration frameworks only when actual workflows justify them.

---

# 100. AI Configuration

Strongly typed configuration may include:

```text
AiOptions
Provider
DefaultGenerationModel
EmbeddingModel
Timeout
MaximumOutputTokens
Temperature
BudgetPolicy
```

Provider-specific configuration remains in Infrastructure.

---

# 101. Temperature

For structured commerce extraction, lower-variance generation is generally preferable.

Exact model parameters should be configuration and evaluation driven.

Do not encode them into Domain logic.

---

# 102. Maximum Output

AI requests should define bounded output sizes.

This helps control:

```text
cost
latency
unexpected output
resource consumption
```

---

# 103. Context Window

Do not send unlimited Catalog or document data to a model.

Use:

```text
retrieval
chunking
summarization where safe
structured context selection
```

Context is a limited resource.

---

# 104. Document Chunking

RAG document ingestion may use chunking.

Chunk strategy should consider:

```text
document structure
semantic boundaries
token size
overlap
metadata
```

There is no universal optimal chunk size.

Evaluation should drive tuning.

---

# 105. Retrieval Metadata

Indexed knowledge chunks should include metadata such as:

```text
DocumentId
SourceType
Version
Category
Language
CreatedAtUtc
UpdatedAtUtc
AccessScope
```

This supports filtering and lineage.

---

# 106. Retrieval Authorization

RAG retrieval must enforce access boundaries.

A model must not receive documents the requesting identity is not allowed to access.

---

# 107. Multilingual Support

Yunu.Commerce AI should support multilingual input where providers permit it.

Initial languages may include:

```text
Portuguese
English
Spanish
```

Canonical commerce values should remain normalized independently of input language.

---

# 108. Localization vs Canonical Data

AI may produce localized descriptions.

Canonical identifiers and structured attributes should remain language-independent where possible.

Example:

```text
ColorCode = BLACK
Localized display = Preto / Black / Negro
```

---

# 109. Failure Semantics

Potential canonical AI failures include:

```text
AiProviderUnavailable
AiRateLimitExceeded
AiTimeout
AiInvalidStructuredOutput
AiSafetyRejected
AiContextTooLarge
AiValidationFailed
AiBudgetExceeded
```

Provider-specific exceptions must be translated.

---

# 110. Degraded Mode

AI failure must not make core deterministic commerce unavailable.

If AI is unavailable:

```text
Catalog CRUD should still work
Pricing should still work
Availability should still work
Freight should still work
```

AI is an enhancement, not a single point of failure for the commerce core.

---

# 111. AI Health

AI provider health may affect AI readiness but should not necessarily affect the liveness of unrelated commerce APIs.

Health checks must reflect service responsibilities.

---

# 112. AI Testing

Testing layers include:

```text
unit tests for workflows
schema validation tests
prompt regression tests
provider adapter tests
fake-provider tests
evaluation datasets
end-to-end enrichment tests
```

---

# 113. Fake AI Provider

A deterministic fake provider should exist for development and tests.

Example behavior:

```text
known input
→ known structured Product Proposal
```

This enables repeatable tests without token cost.

---

# 114. Provider Contract Tests

Azure and Google adapters should have provider-specific integration tests against appropriate development/sandbox resources where practical.

---

# 115. Architecture Tests

Architecture tests should enforce:

```text
Domain projects do not reference AI SDKs

Catalog Domain does not reference AI Application

AI Application does not reference concrete provider SDKs

AI Infrastructure implements provider-neutral ports

AI tools do not reference database clients directly
```

---

# 116. First Vertical Slice

The first GenAI slice should be deliberately narrow:

```text
Natural-language Product registration
        │
        ▼
Structured Product Proposal
        │
        ▼
Catalog validation
        │
        ▼
Create Product
        │
        ▼
MongoDB
        │
        ▼
Outbox
        │
        ▼
Kafka
        │
        ▼
Elasticsearch
```

This proves the complete architecture.

---

# 117. First AI Input

Initial API request may conceptually contain:

```text
NaturalLanguageDescription
Optional external reference
Optional source metadata
```

Do not begin with every possible multimodal input.

---

# 118. First AI Output

Initial output should remain focused:

```text
Product Name
Brand suggestion
Category suggestion
Description
Attributes
Specifications
Candidate SKU data
Warnings
```

Every field must be validated.

---

# 119. First Provider

Only one provider needs to be implemented first.

The port must make adding the second provider straightforward.

A provider choice should be recorded by ADR.

---

# 120. First Search/Embedding Slice

After Product registration works, add:

```text
ProductCreated/ProductUpdated
        │
        ▼
Embedding Worker
        │
        ▼
Embedding Provider
        │
        ▼
Elasticsearch dense_vector
```

Then implement semantic/hybrid search.

---

# 121. Later AI Capabilities

Future capabilities may include:

```text
automatic product enrichment
duplicate product detection
category classification
attribute extraction from images
supplier-document extraction
semantic search
natural-language shopping
catalog quality analysis
seller onboarding assistance
support copilots
freight analysis
availability anomaly detection
```

Each capability must have a clear owner and deterministic safety boundary.

---

# 122. AI ADRs

Important AI decisions requiring ADRs include:

```text
Provider-neutral AI abstraction

First AI provider

Azure vs Google provider strategy

Structured output strategy

Prompt versioning strategy

Elasticsearch as initial vector store

Human approval policy

AI execution persistence

AI cost tracking

Direct SDK vs agent framework

AI tool security policy
```

---

# 123. Copilot Implementation Rules

When GitHub Copilot generates AI code, it must:

```text
never add provider SDK references to Domain

never allow an LLM to write directly to a database

never allow AI to bypass Application commands

treat AI output as untrusted

prefer structured output

validate deserialized output

use CancellationToken for I/O

use async APIs

use strongly typed options

keep prompts versioned

record provider/model metadata

record token usage where available

add timeouts

add bounded retry for transient failures

avoid logging secrets or unrestricted prompts

provide a fake AI adapter for tests

write tests for AI-to-Domain validation

avoid speculative agent frameworks
```

---

# 124. Forbidden AI Patterns

Forbidden unless explicitly approved by ADR:

```text
LLM directly connected to MongoDB

LLM directly connected to SQL

LLM directly changing Price

LLM directly changing Availability

Provider DTO used as Domain Entity

Hardcoded API key

Hardcoded provider throughout Application

Unvalidated JSON response persisted as Product

Prompt strings duplicated across handlers

Unlimited tool access

Unlimited context injection

Silent provider fallback

Unbounded AI retry

AI response treated as authoritative evidence

Elasticsearch vector index treated as source of truth
```

---

# 125. Architecture Questions Before Implementation

Before implementing the AI infrastructure, explicitly decide:

```text
Which provider is implemented first?

Azure AI or Google AI?

Which model is used for structured Product extraction?

Which embedding model is used?

Will Elasticsearch be the initial vector store?

Where are prompts stored?

How are prompt versions identified?

Where is AI execution metadata stored?

Are full prompts/responses persisted?

What redaction policy applies?

Which Product fields may AI propose?

Which fields require human approval?

How are GTIN/EAN values verified?

What is the maximum token budget per request?

What timeout applies?

What fallback policy applies?

When is the second provider activated?

Will the first workflow be synchronous or asynchronous?

What evaluation dataset is required before production?
```

---

# 126. Initial AI Implementation Sequence

Recommended sequence:

```text
1. Define provider-neutral AI contracts

2. Define structured Product Proposal schema

3. Create fake AI provider

4. Implement Product Enrichment use case

5. Add validation pipeline

6. Integrate Catalog Application

7. Implement first real AI provider adapter

8. Add provider configuration

9. Add timeout and resilience

10. Add execution metadata and token/cost tracking

11. Add prompt versioning

12. Add tests/evaluation dataset

13. Persist Product through Catalog

14. Publish Catalog event through Outbox/Kafka

15. Project Product into Elasticsearch

16. Add embedding abstraction

17. Generate Product embeddings

18. Add semantic/hybrid search

19. Implement second AI provider adapter

20. Evaluate agent/tool orchestration only after the deterministic workflow works
```

---

# 127. End-to-End Target

The initial architecture should eventually support:

```text
User:
"Cadastre uma TV Samsung OLED 55 4K 120Hz..."

                │
                ▼

        Yunu.Commerce API

                │
                ▼

       AI Application Workflow

                │
                ▼

      IGenerativeAiProvider

          ┌─────┴─────┐
          ▼           ▼
       Azure        Google

          └─────┬─────┘
                ▼

     Structured Product Proposal

                │
                ▼

       Application Validation

                │
                ▼

          Catalog Domain

                │
                ▼

             MongoDB

                │
                ▼

              Outbox

                │
                ▼

              Kafka

                │
        ┌───────┴────────┐
        ▼                ▼
 Search Projection   Embedding Worker
        │                │
        ▼                ▼
 Elasticsearch     Embedding Provider
        │                │
        └───────┬────────┘
                ▼
       Search + Vector Index
```

---

# 128. Core Rule

The core AI Architecture rule is:

> Generative AI is an intelligent adapter and reasoning capability around the application, not the owner of business truth.

AI may:

```text
understand
extract
classify
suggest
enrich
retrieve
summarize
orchestrate approved tools
```

But deterministic Application and Domain rules decide what becomes canonical commerce state.

---

# 129. Final Principle

Yunu.Commerce AI Architecture must remain:

```text
provider-independent
model-independent where practical
structured
validated
observable
cost-aware
secure
testable
evaluated
RAG-capable
tool-capable
multimodal-ready
human-reviewable
Domain-protected
cloud-adaptable
```

Azure models may change.

Google models may change.

Prompt strategies may change.

Embedding models may change.

Vector stores may change.

Agent frameworks may change.

The commerce Domains and their business invariants must remain protected.
