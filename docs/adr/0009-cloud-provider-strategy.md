# ADR-0009: Cloud Provider Strategy

- **Status:** Accepted
- **Date:** 2026-08-11
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Cloud infrastructure, portability, managed services, identity, secrets, deployment and provider boundaries

## 1. Context

Yunu.Commerce is designed as a cloud-native commerce and Generative AI platform.

The architecture includes:

```text
.NET services
ASP.NET Core APIs
Background Workers
Docker containers
Kubernetes
Kafka
MongoDB
Relational databases
Redis
Elasticsearch
Object Storage
Generative AI
Embeddings
Observability
CI/CD
Secrets and identity
```

The platform needs a practical primary cloud environment while avoiding unnecessary coupling of the business core to a single cloud provider.

Generative AI may use services from Microsoft Azure and Google independently of the primary infrastructure provider.

## 2. Decision

Microsoft Azure will be the initial primary cloud platform for Yunu.Commerce.

However:

> Yunu.Commerce will be Azure-first, not Azure-locked.

DDD, Application contracts and core business logic remain cloud-independent.

Cloud-specific capabilities are implemented through Infrastructure adapters and deployment configuration.

## 3. Primary Cloud

The initial production-oriented cloud target is:

```text
Microsoft Azure
```

Reasons include:

```text
strong .NET integration
managed Kubernetes
managed identity
Key Vault
Azure Monitor / Application Insights
container registry
networking
managed database options
AI services
enterprise adoption
```

## 4. Cloud Boundary

Cloud SDKs must not leak into Domain projects.

Forbidden:

```text
Catalog.Domain
    │
    ▼
Azure SDK
```

Correct:

```text
Application
    │
    ▼
Port
    │
    ▼
Azure Infrastructure Adapter
```

The same rule applies to Google Cloud SDKs.

## 5. Initial Platform Direction

Conceptually:

```text
                    Azure
                      │
        ┌─────────────┼─────────────┐
        ▼             ▼             ▼
       AKS      Container Registry  Key Vault
        │
        ▼
Yunu.Commerce Workloads
        │
        ├── APIs
        ├── Workers
        ├── Kafka Consumers
        ├── Outbox Publishers
        └── AI Workers
```

Supporting managed or externally hosted data services connect through Infrastructure.

## 6. Containers

Yunu.Commerce applications will be containerized with Docker.

The same application container should run in:

```text
local development
CI integration environments
Azure Kubernetes Service
other Kubernetes environments where feasible
```

Cloud-specific behavior should not be compiled into the business core.

## 7. Kubernetes

Azure Kubernetes Service (AKS) is the initial orchestration target for production-like deployments.

Kubernetes provides:

```text
deployment
horizontal scaling
service discovery
configuration
health probes
rolling updates
resource management
workload isolation
```

Not every component must immediately be deployed as an independent microservice.

## 8. Modular Monolith Compatibility

The initial Yunu.Commerce architecture may remain a Modular Monolith while running in containers.

Example:

```text
AKS
 │
 ▼
Yunu.Commerce API
 │
 ├── Catalog
 ├── Sellers
 ├── Offers
 ├── Pricing
 ├── Availability
 ├── Fulfillment
 └── Freight
```

Workers may be deployed independently where operationally useful.

## 9. Microservice Evolution

Bounded Contexts may later become independent deployments when justified by:

```text
scaling
fault isolation
deployment cadence
team ownership
security
resource profile
```

Cloud infrastructure must support this evolution without requiring Domain redesign.

## 10. Container Registry

Azure Container Registry is the initial container image registry.

Conceptually:

```text
GitHub Actions
      │
      ▼
Build/Test
      │
      ▼
Docker Image
      │
      ▼
Azure Container Registry
      │
      ▼
AKS
```

## 11. CI/CD

GitHub Actions is the preferred initial CI/CD platform.

Pipelines should support:

```text
restore
build
unit tests
architecture tests
integration tests
container build
security scanning
image publishing
deployment
```

Production deployment policies may evolve independently.

## 12. Infrastructure as Code

Cloud resources should eventually be defined through Infrastructure as Code.

Preferred direction:

```text
Bicep
```

or another explicitly approved IaC technology.

Manual portal configuration should not become the long-term source of infrastructure truth.

## 13. Secrets

Secrets must not be committed to Git.

Production secrets and sensitive configuration will use Azure Key Vault where appropriate.

Examples:

```text
database credentials
external API secrets
AI provider credentials
certificates
signing material
```

## 14. Managed Identity

Azure Managed Identity should be preferred over static credentials where supported.

Conceptually:

```text
AKS Workload
    │
    ▼
Managed Identity
    │
    ▼
Azure Resource
```

This reduces secret distribution.

## 15. Local Secrets

Local development may use:

```text
.NET User Secrets
environment variables
local development configuration
```

Never commit real production credentials into:

```text
appsettings.json
docker-compose.yml
source code
Markdown documentation
```

## 16. Identity and Access

Azure Entra ID is the preferred identity platform for Azure-hosted enterprise authentication scenarios.

Application authorization remains expressed through application/domain concepts rather than cloud SDK calls.

## 17. API Security

The architecture may use:

```text
OAuth 2.0
OpenID Connect
JWT
RBAC / policy-based authorization
```

depending on API consumers and deployment requirements.

## 18. API Management

Azure API Management may be introduced for public/enterprise API governance.

Potential responsibilities:

```text
gateway
authentication enforcement
rate limiting
routing
API versioning
policies
observability
```

It is not required for the first local vertical slice.

## 19. Network Security

Production infrastructure should favor private networking where practical.

Potential controls include:

```text
VNets
private endpoints
network policies
firewall rules
restricted ingress
restricted egress
```

Databases, Redis and internal infrastructure should not be publicly exposed without explicit justification.

## 20. Relational Database

The relational persistence layer remains abstracted from Domain.

Azure-hosted options may include:

```text
Azure SQL
PostgreSQL managed services
```

The final engine should be selected based on implementation requirements.

The architecture does not require Domain changes if the relational provider changes.

## 21. MongoDB

MongoDB is used where document persistence fits the Bounded Context.

Deployment may initially use:

```text
local Docker for development
managed MongoDB-compatible/provider service for cloud
```

The exact managed hosting decision is an Infrastructure concern.

## 22. Redis

Redis remains the distributed cache/low-latency projection platform defined by ADR-0006.

The cloud implementation may use an Azure-managed Redis-compatible service or another approved deployment.

Application ports remain provider-neutral.

## 23. Elasticsearch

Elasticsearch remains the Search projection technology defined by ADR-0007.

The architecture must not depend on whether Elasticsearch is:

```text
self-hosted
managed by Elastic
hosted through cloud infrastructure
```

Search ports remain stable.

## 24. Kafka

Apache Kafka remains the event-streaming abstraction defined by ADR-0004.

The deployment may evolve between:

```text
local Kafka
managed Kafka
Kafka-compatible cloud services
```

Application and Domain code remain independent of the hosting choice.

## 25. Object Storage

Binary assets and large files should use object storage rather than transactional databases.

Potential content:

```text
product images
documents
supplier files
AI ingestion artifacts
exports
```

Azure Blob Storage is the initial Azure implementation candidate.

Application code should depend on an object-storage Port.

## 26. Object Storage Port

Conceptually:

```text
IObjectStorage
      │
      ├── AzureBlobStorageAdapter
      └── FutureProviderAdapter
```

Domain models should store stable asset references/identifiers rather than Azure SDK objects.

## 27. Generative AI

Generative AI is explicitly multi-provider capable.

Supported architecture:

```text
IGenerativeAiProvider
        │
        ├── Azure AI
        └── Google AI
```

The primary cloud being Azure does not require all AI workloads to use Azure.

See ADR-0008.

## 28. Google AI

Google AI may be selected for specific workloads based on:

```text
model capability
quality
latency
cost
context window
multimodal support
availability
```

Google credentials and SDKs remain isolated in the Google AI adapter.

## 29. Azure AI

Azure AI may be selected for workloads where it provides advantages such as:

```text
Azure enterprise integration
identity
network controls
regional deployment
model availability
governance
```

Provider choice is made at the AI Infrastructure boundary.

## 30. Provider Mixing

Yunu.Commerce may run:

```text
Infrastructure → Azure

Generative model → Google

Embedding model → Azure
```

or another approved combination.

Cloud infrastructure provider and AI model provider are separate architectural decisions.

## 31. Cloud Portability

Portability does not mean pretending cloud services are identical.

The goal is:

```text
protect business logic
protect application contracts
isolate provider-specific behavior
keep deployment replaceable
```

It is acceptable for Infrastructure adapters to use powerful provider-specific features.

## 32. Lowest Common Denominator

Yunu.Commerce will not deliberately reduce every cloud capability to the lowest common denominator merely for theoretical portability.

Provider-specific optimization is allowed behind stable Ports.

## 33. Observability Standard

OpenTelemetry is the primary vendor-neutral telemetry standard.

The platform should emit:

```text
logs
metrics
traces
```

through OpenTelemetry-compatible instrumentation where practical.

## 34. Azure Observability

Azure Monitor and Application Insights may receive production telemetry.

Conceptually:

```text
Yunu.Commerce
      │
      ▼
OpenTelemetry
      │
      ▼
Azure Monitor / Application Insights
```

This preserves instrumentation portability.

## 35. Structured Logging

Logs should be structured and include context such as:

```text
CorrelationId
TraceId
BoundedContext
Operation
EventId
EntityId where safe
Duration
Result
```

Secrets and sensitive payloads must not be logged.

## 36. Health Checks

Executable workloads should expose appropriate:

```text
liveness
readiness
startup
```

health behavior.

A dependency failure should be classified correctly rather than causing indiscriminate restarts.

## 37. Kubernetes Probes

AKS deployments should map application health endpoints to Kubernetes probes.

Examples:

```text
liveness
→ Is the process alive?

readiness
→ Can this workload currently serve its intended traffic?
```

## 38. Resilience

External calls should use bounded resilience policies.

Potential mechanisms:

```text
timeouts
retries
circuit breakers
rate limiting
bulkheads
```

Policies must be appropriate to the dependency and operation.

## 39. Configuration

Configuration should follow environment-based deployment practices.

Examples:

```text
appsettings.json
appsettings.Development.json
environment variables
Key Vault references
Kubernetes configuration
```

No environment-specific business logic belongs in Domain.

## 40. Environments

Initial environment model may include:

```text
Local
Development
Staging
Production
```

Environment topology can evolve without changing Domain code.

## 41. Local Development

Local development should run core infrastructure through Docker Compose where practical.

Conceptually:

```text
Docker Compose
│
├── MongoDB
├── Relational Database
├── Kafka
├── Redis
└── Elasticsearch
```

Cloud services should not be mandatory for ordinary Domain development.

## 42. AI Local Development

AI development may use:

```text
fake deterministic adapter
Azure provider integration profile
Google provider integration profile
```

Most automated tests should not require paid external AI calls.

## 43. Developer Experience

A developer should be able to clone Yunu.Commerce and run a meaningful local environment without manually provisioning an entire Azure subscription.

Cloud integration is layered on top of the same architecture.

## 44. Infrastructure Projects

Provider-specific code belongs in Infrastructure/adapters.

Potential future structure:

```text
Yunu.Commerce.Infrastructure.Azure
Yunu.Commerce.AI.Azure
Yunu.Commerce.AI.Google
```

or equivalent modules consistent with `06-solution-structure.md`.

Do not create projects before a real implementation requires them.

## 45. Cloud SDK Isolation

Architecture tests should ensure:

```text
Domain → no Azure SDK
Domain → no Google SDK

Application → no concrete cloud infrastructure dependency
```

Provider packages belong to outer layers.

## 46. Data Residency

Production deployment must account for data residency and regulatory requirements.

Regions should be selected based on:

```text
customers
legal requirements
latency
service availability
cost
disaster recovery
```

No global replication strategy is assumed initially.

## 47. Backup

Canonical stores require explicit backup/recovery strategies.

Examples:

```text
Catalog database
Pricing database
Availability database
Seller database
Fulfillment database
```

Derived stores such as Redis and Elasticsearch may often be rebuilt, but recovery strategy must still be documented.

## 48. Disaster Recovery

Disaster recovery requirements will evolve with production objectives.

Future decisions should define:

```text
RPO
RTO
backup retention
regional recovery
restore testing
```

Do not invent expensive multi-region infrastructure before requirements justify it.

## 49. Scaling

Workloads should scale independently when deployment boundaries justify it.

Potential high-scale components:

```text
Availability consumers
Search projection workers
AI workers
Catalog API
Search API
Outbox publishers
```

Kubernetes horizontal scaling may be used based on relevant metrics.

## 50. AI Worker Scaling

AI workloads require special scaling controls because provider calls have:

```text
cost
rate limits
token limits
latency
```

Do not scale AI consumers solely based on Kafka lag without considering provider quotas and budget.

## 51. Cost Management

Cloud architecture must be cost-observable.

Track costs by categories such as:

```text
compute
database
Kafka
Redis
Elasticsearch
storage
network
AI inference
embeddings
observability
```

Architecture decisions should consider total operational cost, not only technical elegance.

## 52. Resource Tagging

Azure resources should use consistent tags when production infrastructure is introduced.

Potential tags:

```text
Application
Environment
BoundedContext
Owner
CostCenter
ManagedBy
```

## 53. Infrastructure Naming

Cloud resource naming conventions should be centralized.

Avoid ad hoc resource names created manually by individual developers.

## 54. Production Access

Production infrastructure access must follow least privilege.

Developers should not routinely require broad owner-level access.

Use:

```text
RBAC
managed identities
scoped roles
auditable access
```

## 55. Secret Rotation

Credential-based secrets that cannot be replaced by managed identity must support rotation without source-code changes.

## 56. Cloud Vendor Failure

The business core should remain testable even when cloud services are unavailable.

This is another reason for Ports and Adapters.

## 57. Exit Strategy

A cloud migration would still be significant operational work.

This ADR does not claim cloud migration is free.

It aims to ensure that moving infrastructure does not require rewriting:

```text
Aggregates
Value Objects
Domain Services
business invariants
Application use cases
```

## 58. Azure-Specific Migration Surface

If Azure were replaced, expected changes would concentrate in:

```text
Infrastructure adapters
IaC
deployment manifests
identity integration
observability exporters
managed service configuration
```

rather than business logic.

## 59. Security Architecture Direction

The target security toolbox includes:

```text
Entra ID
OAuth / OIDC
JWT
Managed Identity
Key Vault
RBAC
API Management
network isolation
TLS
least privilege
```

Specific implementations will be added as corresponding workloads are built.

## 60. No Premature Infrastructure

The repository scaffold should represent architecture without provisioning every possible cloud resource immediately.

Initial priority:

```text
Domain boundaries
Application boundaries
local infrastructure
vertical slices
automated tests
```

Cloud resources are added as the application requires them.

## 61. Initial Cloud Implementation Sequence

Recommended sequence:

```text
1. Build complete local architecture

2. Containerize API and Workers

3. Add CI build/test pipeline

4. Create Azure resource baseline with IaC

5. Create Azure Container Registry

6. Provision AKS

7. Configure workload identity / managed identity

8. Configure Key Vault

9. Deploy first API/Worker

10. Connect managed persistence services

11. Configure OpenTelemetry export

12. Add Application Insights / Azure Monitor

13. Add ingress/API gateway strategy

14. Harden networking

15. Add autoscaling and cost controls
```

## 62. First Cloud Vertical Slice

The first Azure deployment should prove:

```text
GitHub
   │
   ▼
GitHub Actions
   │
   ▼
Build + Tests
   │
   ▼
Docker
   │
   ▼
Azure Container Registry
   │
   ▼
AKS
   │
   ▼
Yunu.Commerce API
```

Then add external dependencies incrementally.

## 63. GenAI Cloud Slice

After the Catalog vertical slice exists:

```text
Catalog API on Azure
       │
       ▼
IGenerativeAiProvider
       │
       ├── Azure AI
       └── Google AI
       │
       ▼
Structured Product Proposal
       │
       ▼
Catalog Domain
```

This proves cloud-provider and AI-provider concerns remain independent.

## 64. Consequences

### Positive

```text
strong Azure/.NET ecosystem fit
clear production target
managed identity
enterprise security capabilities
AKS scalability
cloud-neutral business core
multi-provider AI support
local development independence
future infrastructure portability
```

### Negative

```text
Azure-specific Infrastructure code still exists
cloud operations require expertise
AKS adds operational complexity
multi-provider AI adds configuration/security work
portability requires architectural discipline
managed services create some operational switching cost
```

These tradeoffs are accepted.

## 65. Alternatives Considered

### Fully Cloud-Agnostic Infrastructure

Rejected as an absolute goal because avoiding all provider-specific capabilities would reduce practical value and increase abstraction cost.

### Azure Lock-In Everywhere

Rejected because Domain and Application should survive provider changes.

### Google Cloud as the Only Platform

Not selected as the initial infrastructure target, while Google remains a valid AI provider and future Infrastructure option.

### Multi-Cloud Active/Active From Day One

Rejected because it would introduce major cost and operational complexity before a demonstrated requirement exists.

### No Kubernetes

Not selected because Kubernetes aligns with the intended containerized architecture and future independent workload scaling.

However, deployment topology may still evolve if operational evidence favors simpler hosting for some workloads.

## 66. Copilot Rules

GitHub Copilot must:

```text
Treat Azure as the initial cloud target, not as the Domain architecture.

Never reference Azure SDKs from Domain.

Never reference Google SDKs from Domain.

Keep cloud SDKs in Infrastructure/adapters.

Prefer provider-neutral Application ports.

Never hardcode credentials.

Never commit secrets.

Prefer Managed Identity on Azure where supported.

Use Key Vault for production secrets where appropriate.

Use OpenTelemetry for vendor-neutral instrumentation.

Containerize executable workloads.

Keep local development runnable without AKS.

Do not create cloud resources manually as the long-term strategy.

Prefer Infrastructure as Code.

Do not introduce multi-cloud complexity without a requirement.

Do not introduce microservices merely because AKS exists.

Use bounded timeouts/resilience for cloud dependencies.

Respect least privilege and network isolation.

Keep AI provider selection independent from infrastructure cloud selection.

Do not let deployment topology change Domain boundaries.
```

## 67. Relationship to Other ADRs

This ADR depends on and completes the architecture established by:

```text
ADR-0001
Use DDD, Clean Architecture and Hexagonal Architecture

ADR-0002
Bounded Context Strategy

ADR-0003
Database per Bounded Context

ADR-0004
Use Kafka for Event-Driven Integration

ADR-0005
Use Transactional Outbox

ADR-0006
Use Redis for Distributed Cache

ADR-0007
Use Elasticsearch for Search Projections

ADR-0008
GenAI Provider Abstraction
```

## 68. Final Decision

Yunu.Commerce adopts Microsoft Azure as its initial primary cloud platform while preserving cloud independence in Domain and Application layers.

Azure will provide the first production-oriented hosting, security, identity, container and observability environment.

Generative AI remains independently pluggable between Azure and Google through provider-neutral Ports and Adapters.

The defining principle is:

> Azure hosts Yunu.Commerce. It does not define Yunu.Commerce.
