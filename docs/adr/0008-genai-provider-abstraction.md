# ADR-0008: GenAI Provider Abstraction

- **Status:** Accepted
- **Date:** 2026-08-11
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Generative AI, embeddings, structured output, tool calling and AI-assisted commerce workflows

## 1. Context

Generative AI is a strategic capability of Yunu.Commerce.

Initial use cases include AI-assisted product registration and enrichment from unstructured input such as:

```text
natural-language descriptions
supplier information
technical specifications
documents
images and media metadata
external product information
```

Future use cases may include:

```text
product enrichment
attribute extraction
category suggestions
description generation
semantic search
embeddings
RAG
commerce assistants
tool calling
catalog quality analysis
seller onboarding assistance
```

Yunu.Commerce may use AI services from different providers, initially including Microsoft Azure and Google.

The Domain must not become coupled to any specific model, SDK, cloud vendor or model naming convention.

## 2. Decision

Yunu.Commerce will introduce provider-neutral AI Ports with provider-specific Adapters.

Conceptually:

```text
Application
    │
    ▼
AI Port
    │
    ├── Azure AI Adapter
    └── Google AI Adapter
```

Provider selection occurs through Infrastructure configuration and dependency injection.

## 3. Fundamental Rule

The central architectural rule is:

> AI proposes. The Application and Domain validate and decide.

Generative AI output is never automatically considered canonical commerce truth.

Example:

```text
User Input
    │
    ▼
Generative AI
    │
    ▼
Product Proposal
    │
    ▼
Application Validation
    │
    ▼
Catalog Domain
    │
    ▼
Canonical Product
```

## 4. AI Is an External Dependency

From the perspective of the business core, an LLM is an external system.

Therefore it belongs behind Hexagonal Architecture ports.

Forbidden:

```text
Catalog.Domain
    │
    ▼
Azure AI SDK
```

Forbidden:

```text
Catalog.Domain
    │
    ▼
Google AI SDK
```

Correct:

```text
Catalog.Application
    │
    ▼
IProductEnrichmentService / AI Port
    │
    ▼
Infrastructure Adapter
```

## 5. Provider-Neutral Language

Application interfaces must describe capabilities rather than vendors.

Avoid:

```text
IAzureOpenAiService
IGeminiService
IGoogleAiProductGenerator
```

Prefer abstractions such as:

```text
IGenerativeAiProvider
IEmbeddingGenerator
IStructuredGeneration
```

Purpose-specific application services may sit above these lower-level ports.

## 6. Provider Adapters

Infrastructure may implement adapters such as:

```text
AzureGenerativeAiProvider
GoogleGenerativeAiProvider

AzureEmbeddingGenerator
GoogleEmbeddingGenerator
```

Provider SDKs and authentication remain isolated inside Infrastructure.

## 7. Initial Provider Strategy

Yunu.Commerce will support an architecture capable of using:

```text
Azure AI
or
Google AI
```

without requiring Domain redesign.

Supporting both does not mean every request must dynamically switch providers.

The initial implementation may configure one provider as active.

## 8. Provider Selection

Provider selection should be configuration-driven.

Conceptually:

```text
AI_PROVIDER=Azure
```

or:

```text
AI_PROVIDER=Google
```

The exact configuration mechanism belongs to the Host and Infrastructure layers.

## 9. Future Routing

Future requirements may justify intelligent routing based on:

```text
cost
latency
model capability
region
availability
task type
quality
token limits
```

Such routing is not required in the first implementation.

It should be introduced only after measurable requirements exist.

## 10. Model Independence

Application and Domain code must not depend on specific model names.

Forbidden:

```text
if model == "some-provider-model-name"
```

inside business logic.

Model/deployment names belong to configuration.

## 11. Capability-Based Configuration

Configuration should describe provider and model capabilities.

Conceptually:

```text
GenerativeAI
├── Provider
├── ChatModel
├── EmbeddingModel
├── Temperature
├── MaxOutputTokens
└── Timeout
```

Provider-specific settings remain in provider-specific Infrastructure options.

## 12. First AI Use Case

The first Yunu.Commerce GenAI vertical slice will assist Product registration.

Example user input:

```text
Cadastre um notebook Lenovo ThinkPad,
32 GB RAM, SSD 1 TB, Intel Core Ultra,
tela 14 polegadas.
```

AI may transform this into a structured proposal.

## 13. Structured Product Proposal

Conceptually:

```json
{
  "name": "Lenovo ThinkPad ...",
  "brand": "Lenovo",
  "category": "Notebook",
  "attributes": {
    "memory": "32 GB",
    "storage": "1 TB SSD",
    "processor": "Intel Core Ultra",
    "screenSize": "14 in"
  }
}
```

This object is a proposal.

It is not yet a Product Aggregate.

## 14. Product Creation Flow

The intended flow is:

```text
Natural Language / Document
          │
          ▼
     AI Endpoint
          │
          ▼
Catalog Application
          │
          ▼
AI Product Extraction Port
          │
          ▼
Azure AI / Google AI
          │
          ▼
Structured Proposal
          │
          ▼
Validation / Normalization
          │
          ▼
CreateProduct Command
          │
          ▼
Catalog Domain
          │
          ▼
Product Aggregate
          │
          ▼
MongoDB + Outbox
```

AI never writes directly to MongoDB.

## 15. Structured Output

Structured output is preferred over parsing unconstrained prose when AI output feeds application workflows.

The application should request explicit schemas/contracts where provider capabilities allow it.

Benefits:

```text
predictable parsing
validation
lower ambiguity
safer automation
better tests
```

## 16. AI DTOs Are Not Domain Entities

AI response contracts belong outside Domain.

Example:

```text
GeneratedProductProposal
```

must be mapped into Domain commands/value objects after validation.

Do not deserialize AI output directly into:

```text
Product Aggregate
```

## 17. Domain Validation

All AI-generated values must pass the same Domain invariants as manually supplied values.

AI receives no privileged path around business rules.

Example:

```text
AI proposes SKU
      │
      ▼
Domain detects invalid SKU rule
      │
      ▼
Reject
```

The AI response does not override the Domain.

## 18. Human Review

Some workflows may require human confirmation before canonical mutation.

Potential flow:

```text
AI Proposal
    │
    ▼
Review
    │
    ├── Edit
    ├── Reject
    └── Approve
          │
          ▼
       Domain
```

The review requirement depends on risk and confidence of each use case.

## 19. Confidence

If a provider returns confidence or equivalent metadata, it may be captured as AI operational metadata.

Do not assume provider confidence is a calibrated business truth.

Yunu.Commerce may define its own evaluation thresholds later.

## 20. Prompt Management

Prompts are versioned application assets.

They should not be scattered as arbitrary strings throughout controllers and handlers.

Conceptually:

```text
Prompts
├── Catalog
│   ├── ProductExtraction
│   └── ProductEnrichment
└── Search
    └── QueryUnderstanding
```

## 21. Prompt Versioning

AI executions should be traceable to a prompt version.

Example:

```text
PromptId
PromptVersion
Provider
Model
```

This enables evaluation and regression analysis.

## 22. System Prompts

System instructions should define:

```text
task
output contract
constraints
business terminology
forbidden assumptions
```

They should not contain secrets.

## 23. Prompt Injection

Untrusted user, supplier or document content must be treated as data, not trusted instructions.

The system must not allow embedded content to override platform policies or tool permissions.

## 24. Tool Calling

AI may invoke explicitly exposed application tools.

Conceptually:

```text
LLM
 │
 ├── SearchCatalog
 ├── GetProduct
 ├── GetPrice
 └── GetAvailability
```

Tools expose controlled application capabilities.

They do not expose databases directly.

## 25. Tool Boundary

Forbidden:

```text
LLM
 │
 ▼
Execute arbitrary SQL
```

Forbidden:

```text
LLM
 │
 ▼
Direct MongoDB access
```

Correct:

```text
LLM
 │
 ▼
Approved Tool Contract
 │
 ▼
Application
 │
 ▼
Domain / Query Port
```

## 26. Mutation Tools

AI-triggered mutation requires stricter controls than read tools.

Example:

```text
CreateProduct
ChangePrice
UpdateAvailability
```

These must execute normal authorization, validation and Domain behavior.

High-impact operations may require explicit human confirmation.

## 27. Tool Allowlist

Only explicitly registered tools are available to an AI workflow.

No automatic discovery of arbitrary internal APIs or infrastructure commands is permitted.

## 28. RAG

Retrieval-Augmented Generation may be used when AI needs grounded commerce context.

Conceptually:

```text
Question
   │
   ▼
Embedding
   │
   ▼
Elasticsearch
   │
   ▼
Relevant Context
   │
   ▼
Generative AI
   │
   ▼
Grounded Response
```

## 29. RAG Sources

Potential retrieval sources include:

```text
product descriptions
product specifications
catalog documentation
approved business documentation
seller information
commerce knowledge
```

Only authorized information may enter retrieval context.

## 30. Vector Search

Elasticsearch is the initial vector retrieval platform.

Embedding vectors may be stored in Search projections.

A dedicated vector database is not required initially.

See ADR-0007.

## 31. Embedding Abstraction

Embedding generation must use a provider-neutral Port.

Conceptually:

```csharp
public interface IEmbeddingGenerator
{
    Task<EmbeddingResult> GenerateAsync(
        string input,
        CancellationToken cancellationToken);
}
```

The final interface may evolve during implementation.

## 32. Embedding Metadata

Persist enough metadata to understand vector compatibility.

Potential metadata:

```text
Provider
Model
EmbeddingVersion
Dimensions
GeneratedAtUtc
SourceContentHash
```

## 33. Embedding Regeneration

Embeddings should regenerate only when semantically relevant source content changes.

A content hash may help detect whether regeneration is necessary.

## 34. Asynchronous AI Processing

Long-running or high-volume AI work should be asynchronous.

Potential flow:

```text
ProductUpdated
      │
      ▼
Kafka
      │
      ▼
Embedding Worker
      │
      ▼
AI Provider
      │
      ▼
Elasticsearch Vector Update
```

## 35. AI Events

Potential future Integration Events include:

```text
ProductEnrichmentRequested
ProductEnrichmentCompleted
ProductEnrichmentFailed

EmbeddingGenerationRequested
EmbeddingGenerated
EmbeddingGenerationFailed
```

Events should only be introduced when real workflows require them.

## 36. AI Operational Persistence

AI execution metadata may be stored separately from canonical commerce data.

Potential fields:

```text
ExecutionId
UseCase
Provider
Model
PromptVersion
StartedAtUtc
CompletedAtUtc
InputTokens
OutputTokens
CostEstimate
Status
ErrorCode
CorrelationId
```

## 37. AI Data Ownership

AI operational persistence owns execution metadata.

It does not become the source of truth for Product, Price, Availability or Seller.

## 38. Cost Observability

AI usage must be measurable.

Metrics should include where provider data allows:

```text
request count
input tokens
output tokens
embedding tokens
latency
failures
estimated cost
use case
provider
model
```

## 39. Cost Controls

Potential future controls include:

```text
per-use-case token limits
per-user limits
rate limits
budget alerts
model routing
cache
batch processing
```

Cost optimization must not bypass correctness or security.

## 40. AI Caching

Deterministic or repeatable AI operations may use caching where safe.

Cache keys must account for:

```text
input
prompt version
model/provider semantics
relevant options
```

Do not return obsolete cached AI results after prompt/schema changes.

## 41. Temperature

Business extraction workflows should generally favor deterministic configuration.

Creative generation may use different settings.

Temperature and equivalent model parameters belong to use-case configuration, not Domain logic.

## 42. Timeout

Every provider call must have a bounded timeout.

AI providers are external dependencies and may fail or become slow.

## 43. Retry

Retries should be limited to suitable transient failures.

Do not blindly retry:

```text
invalid prompt
schema validation failure
content-policy rejection
invalid credentials
permanent request errors
```

## 44. Rate Limits

Provider rate limits must be handled explicitly.

High-volume asynchronous workflows should use:

```text
bounded concurrency
backpressure
retry-after handling
queueing
```

## 45. Circuit Breaking

Resilience policies may prevent repeatedly calling an unavailable provider.

Provider failures must not cascade uncontrollably through commerce APIs.

## 46. Provider Fallback

Automatic provider fallback is not enabled by default for every AI request.

Different providers/models can produce materially different results.

If fallback is introduced, the use case must define:

```text
compatibility
schema guarantees
quality requirements
cost implications
audit metadata
```

## 47. Data Privacy

Only data required for the AI use case should be sent to external AI providers.

Sensitive information must not be included unnecessarily.

Provider configuration must follow organizational privacy and compliance requirements.

## 48. Secrets

API keys and credentials must never be:

```text
hardcoded
stored in prompts
committed to Git
logged
```

Use managed identity or secret-management infrastructure where supported.

## 49. Cloud Credentials

Azure deployments should prefer managed identity where supported.

Google deployments should use appropriate workload/service identity mechanisms.

The specific cloud strategy is governed by ADR-0009.

## 50. Logging

Do not indiscriminately log full prompts or model responses.

Logging should prioritize:

```text
ExecutionId
UseCase
Provider
Model
PromptVersion
Token usage
Latency
Status
CorrelationId
```

Prompt/response capture requires explicit safe-data policy.

## 51. Observability

AI calls should participate in OpenTelemetry traces where practical.

A trace may connect:

```text
HTTP request
→ Application use case
→ AI provider call
→ Domain operation
→ persistence
→ event publication
```

## 52. Evaluation

AI behavior must be evaluated, not assumed correct.

Evaluation datasets should eventually cover representative commerce examples.

Potential metrics:

```text
field extraction accuracy
category accuracy
attribute normalization accuracy
schema validity
hallucination rate
latency
cost
```

## 53. Regression Testing

Prompt or model changes should be tested against an evaluation set before production rollout for important workflows.

Changing the model can be a behavioral change even when the C# code is unchanged.

## 54. Hallucinations

AI-generated facts not grounded in supplied or retrieved data must not silently enter canonical commerce state.

For extraction use cases:

> Missing information should remain missing rather than be invented.

## 55. Product Attribute Provenance

Future enrichment may record provenance.

Conceptually:

```text
Attribute
Value
Source
AIGenerated
Reviewed
```

This can help distinguish supplier-provided facts from inferred suggestions.

## 56. Search Query Understanding

AI may later translate natural-language shopping intent into structured Search intent.

Example:

```text
"quero um notebook leve até 7 mil para programar"
```

may become:

```text
Category = Notebook
MaxPrice = 7000
Intent = Programming
Preference = Lightweight
```

The structured query is then executed through Search, not invented by the LLM.

## 57. Commerce Assistant

Future architecture may support:

```text
Customer
   │
   ▼
Commerce AI Orchestrator
   │
   ├── SearchCatalog
   ├── GetProduct
   ├── GetPrice
   ├── GetAvailability
   └── GetFreight
```

The assistant composes approved capabilities through tools.

## 58. No Direct AI Database Access

AI providers and AI agents must never receive unrestricted direct database credentials.

All access occurs through controlled application contracts.

## 59. Authorization

Tool execution must respect the authorization context of the caller.

An LLM cannot elevate permissions.

## 60. Provider SDK Isolation

Provider SDK references belong only in provider adapter projects.

Example:

```text
Yunu.Commerce.AI.Azure
Yunu.Commerce.AI.Google
```

or equivalent Infrastructure modules.

The final project structure is governed by the solution architecture.

## 61. Semantic Kernel / AI Frameworks

Frameworks such as orchestration or agent libraries may be evaluated as implementation tools.

They must not become the architectural boundary.

Application ports remain owned by Yunu.Commerce.

This allows frameworks to be replaced without rewriting the Domain.

## 62. Native SDK vs Framework

Use the simplest implementation that satisfies the use case.

A direct provider SDK may be preferable for simple structured generation.

An orchestration framework may be justified when workflows require:

```text
multiple tools
multi-step orchestration
memory/state
complex RAG
agent workflows
```

Do not introduce an agent framework merely because the project uses AI.

## 63. AI Provider Contract Testing

Every provider adapter should pass common behavioral contract tests where applicable.

Example:

```text
Given valid product input
When structured extraction is requested
Then a schema-compatible proposal is returned
```

This helps ensure Azure and Google adapters satisfy the same application expectations.

## 64. Provider-Specific Features

Provider-specific optimizations are allowed inside adapters.

However, they must not leak into Domain contracts.

If a feature fundamentally changes application semantics, it requires explicit architectural review.

## 65. Local Development

Developers should be able to run the platform without mandatory AI calls for every test.

Strategies include:

```text
fake AI adapter
deterministic test adapter
recorded safe fixtures
provider integration test profile
```

The fake adapter must not pretend to validate real provider behavior.

## 66. Integration Tests

Provider integration tests should be separated from fast unit tests because they may:

```text
cost money
require credentials
have rate limits
depend on external availability
```

## 67. Domain Unit Tests

Domain tests must require no AI provider.

This is a key architectural proof that AI is outside the Domain.

## 68. AI Application Tests

Application tests should verify:

```text
AI proposal mapping
schema validation
invalid output handling
Domain rejection
provider timeout
provider failure
cancellation
human-review flow where applicable
```

## 69. Architecture Tests

Architecture tests should enforce:

```text
Domain does not reference Azure AI SDKs.

Domain does not reference Google AI SDKs.

Application does not reference concrete provider SDKs.

Provider adapters implement provider-neutral ports.

AI response DTOs do not become Domain Aggregates.

AI infrastructure cannot bypass Application/Domain mutation paths.
```

## 70. Initial Implementation Sequence

Recommended implementation:

```text
1. Define provider-neutral AI contracts

2. Define ProductExtraction request/response contracts

3. Define prompt/template structure

4. Implement deterministic fake provider

5. Implement Azure AI adapter

6. Implement Google AI adapter

7. Add provider selection through configuration

8. Add structured output validation

9. Connect AI Product proposal to Catalog Application

10. Validate through Catalog Domain

11. Persist Product + Outbox

12. Publish ProductCreated

13. Project Product to Elasticsearch

14. Add telemetry and cost metadata

15. Add embeddings and semantic Search
```

## 71. First GenAI Vertical Slice

The first complete AI flow should be:

```text
Natural-language Product Input
           │
           ▼
       Catalog API
           │
           ▼
Catalog Application
           │
           ▼
Product Extraction Port
           │
           ▼
Azure AI OR Google AI
           │
           ▼
Structured Product Proposal
           │
           ▼
Validation
           │
           ▼
Catalog Domain
           │
           ▼
Product Aggregate
           │
           ▼
MongoDB + Outbox
           │
           ▼
Kafka
           │
           ▼
Elasticsearch
```

This proves that GenAI participates in the architecture without owning it.

## 72. Second GenAI Vertical Slice

After Product registration:

```text
ProductCreated / ProductUpdated
           │
           ▼
Kafka
           │
           ▼
Embedding Worker
           │
           ▼
IEmbeddingGenerator
           │
           ├── Azure
           └── Google
           │
           ▼
Elasticsearch Vector
```

## 73. Third GenAI Vertical Slice

Then:

```text
Natural-language Search
          │
          ▼
Query Understanding / Embedding
          │
          ▼
Hybrid Elasticsearch Search
          │
          ▼
Relevant Products
```

## 74. Fourth GenAI Vertical Slice

Then:

```text
Commerce Assistant
       │
       ▼
LLM
       │
       ├── Search
       ├── Pricing
       ├── Availability
       └── Freight
```

through approved application tools.

## 75. Consequences

### Positive

```text
Azure/Google portability
testable AI boundaries
Domain independence
controlled AI adoption
safer structured generation
future model replacement
RAG support
semantic Search support
cost observability
provider comparison
```

### Negative

```text
additional abstractions
provider capability differences
mapping layers
prompt/version governance
AI evaluation requirements
external service cost
non-deterministic behavior
additional security concerns
```

These tradeoffs are accepted.

## 76. Alternatives Considered

### Direct Azure AI Calls Throughout Application

Rejected because it creates provider coupling.

### Direct Google AI Calls Throughout Application

Rejected for the same reason.

### AI Logic Inside Domain

Rejected because probabilistic external inference must not define deterministic Domain invariants.

### AI Writes Directly to Databases

Rejected because it bypasses Application orchestration and Domain validation.

### One Universal AI Interface for Every Possible Capability

Rejected.

Ports should remain coherent and may be split by capability as real requirements emerge.

### Agent-First Architecture

Rejected as the initial approach.

Agents are a possible orchestration technique, not the foundation of Yunu.Commerce.

## 77. Copilot Rules

GitHub Copilot must:

```text
Never reference Azure or Google AI SDKs from Domain.

Never put provider/model names in Domain logic.

Never allow AI to write directly to databases.

Never deserialize AI output directly into an Aggregate.

Treat AI output as an untrusted proposal.

Validate structured output.

Route canonical mutations through Application and Domain.

Prefer structured output for automation.

Keep prompts versioned and centralized.

Do not hardcode credentials.

Use provider-neutral Ports.

Keep provider SDKs in Adapters/Infrastructure.

Use bounded timeout and cancellation.

Use bounded retries only for transient failures.

Add AI usage telemetry.

Do not log sensitive prompts/responses by default.

Do not invent missing product facts.

Keep tool calling behind an explicit allowlist.

Never give an LLM unrestricted SQL/MongoDB access.

Respect caller authorization during tool execution.

Do not introduce an agent framework without a concrete need.

Keep Domain unit tests independent of AI.
```

## 78. Relationship to Other ADRs

This ADR depends on:

```text
ADR-0001
Use DDD, Clean Architecture and Hexagonal Architecture

ADR-0002
Bounded Context Strategy

ADR-0004
Use Kafka for Event-Driven Integration

ADR-0005
Use Transactional Outbox

ADR-0007
Use Elasticsearch for Search Projections
```

It complements:

```text
ADR-0006
Use Redis for Distributed Cache

ADR-0009
Cloud Provider Strategy
```

## 79. Final Decision

Yunu.Commerce adopts a provider-neutral Generative AI architecture based on Hexagonal Ports and Adapters.

Azure AI and Google AI may implement the same application capabilities without becoming dependencies of the Domain.

Generative AI can extract, enrich, retrieve, recommend and orchestrate approved tools, but canonical commerce mutations always pass through deterministic Application and Domain rules.

The defining principle is:

> AI proposes and assists. The Domain remains the authority.
