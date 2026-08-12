# Bootstrap Solution — Architectural Assumptions

This document records every assumption made by `tools/bootstrap-solution.ps1` where
`docs/architecture/06-solution-structure.md` and `.github/copilot-instructions.md`
did not enumerate an explicit, unambiguous detail.

No assumption listed here introduces business logic, Domain concepts, or
provider-specific implementation into Domain or Application layers.

Each assumption below is cross-referenced against the accepted ADRs
(`docs/adr/0001` through `docs/adr/0009`) to confirm it does not conflict with
an architectural decision already made for the platform.

---

## 1. Target Framework

**Assumption:** `net9.0` for every project via `Directory.Build.props`.

Justification: §37 of `06-solution-structure.md` states the framework "must be
explicitly selected" but does not name a version. `net9.0` is the current .NET
release as of this writing.

**ADR Traceability:** ADR-0001 §5-§6 requires the Domain to remain framework-independent
regardless of the concrete target framework chosen; selecting a single central
`net9.0` via `Directory.Build.props` does not violate this, since no ASP.NET Core,
EF Core, or other forbidden dependency is introduced by the TFM alone.

## 2. `global.json` SDK version

**Assumption:** `"version": "9.0.100"`, `"rollForward": "latestFeature"`.

If the installed SDK differs, adjust `global.json` accordingly; `rollForward` allows
matching within the same feature band without requiring an exact patch match.

**ADR Traceability:** Not directly addressed by any ADR; purely a tooling/reproducibility
concern with no architectural boundary implication.

## 3. Test framework

**Assumption:** xUnit (`xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`).

Not specified in the documentation; xUnit is the de facto standard for modern .NET
solutions and integrates cleanly with `dotnet test`.

**ADR Traceability:** ADR-0001 §7-§9 requires Domain/Application testability without
Infrastructure; xUnit test projects reference only the module's own Domain or
Application project, honoring that isolation.

## 4. Architecture testing library

**Assumption:** `NetArchTest.Rules`.

Chosen as the smallest, dependency-light library capable of enforcing the rules
listed in §41 of `06-solution-structure.md` (Domain/Application/Infrastructure
isolation, vendor SDK isolation, cross-context Domain isolation) without
introducing a heavier framework.

**ADR Traceability:** Directly operationalizes:
- ADR-0001 §4 and §6 (Domain dependency direction and forbidden Domain dependencies)
- ADR-0002 §2/§9 (Bounded Context ownership and no cross-context Domain references)
- ADR-0004 §5 (Kafka must remain outside Domain)
- ADR-0008 §4 (AI SDKs must remain outside Domain)
- ADR-0009 §4 (Cloud SDKs must remain outside Domain)

The generated `DependencyRuleTests.cs` encodes checks for all of the above.

## 5. Health checks

**Assumption:** Built-in ASP.NET Core health checks (`AddHealthChecks()`,
`/health/live`, `/health/ready`) with no external dependency checks wired yet.

No infrastructure adapters exist yet (Mongo, SQL, Kafka, Redis, Elasticsearch), so
there is nothing meaningful to health-check beyond process liveness/readiness at
this phase.

**ADR Traceability:** Consistent with ADR-0003 (persistence not yet implemented),
ADR-0004 (Kafka not yet wired), ADR-0006 (Redis not yet wired), ADR-0007
(Elasticsearch not yet wired). Adding health checks for uninstantiated adapters
would be speculative infrastructure, contrary to §48 of `06-solution-structure.md`.

## 6. OpenTelemetry bootstrap

**Assumption:** `Yunu.Commerce.Observability.AddYunuObservability(serviceName)`
registers tracing and metrics with a **console exporter only**.

No OTLP collector endpoint is defined in this phase (§44 of `06-solution-structure.md`
mentions an OpenTelemetry Collector only as a *possible future* local infrastructure
component). Structured logging is left to the default `Microsoft.Extensions.Logging`
provider; no logging exporter package was added to avoid speculative infrastructure
dependencies.

**ADR Traceability:** ADR-0009 §4/§7 keeps cloud-specific telemetry (e.g. Azure Monitor
/ Application Insights exporters) out of the core bootstrap, preserving the
"Azure-first, not Azure-locked" principle — the console exporter has no cloud
provider coupling and can be replaced later by an Azure or vendor-neutral OTLP
exporter without touching Domain or Application code.

## 7. API Host style

**Assumption:** ASP.NET Core Minimal APIs (`Program.cs` top-level statements), no
MVC Controllers.

§7 of `06-solution-structure.md` does not mandate a specific API style. Minimal
APIs are the smallest valid implementation satisfying "HTTP endpoints, OpenAPI,
middleware, health checks."

**ADR Traceability:** ADR-0001 §10 confirms Hosts are composition roots and must not
contain Domain rules; the generated `Program.cs` only performs DI composition and
module registration, with no business logic, honoring this rule regardless of API
style chosen.

## 8. Worker Host style

**Assumption:** Generic Host with a single placeholder `BackgroundService`
(`Worker`) emitting a heartbeat log every 5 minutes. No Kafka consumer, Outbox
processor, or Inbox processor is wired yet, since introducing them would require
Confluent.Kafka — forbidden at this phase per the project's constraints.

**ADR Traceability:** Directly deferred pending ADR-0004 (Kafka consumers) and
ADR-0005 (Outbox/Inbox processors) implementation. Both ADRs describe Kafka and
Outbox processing as Infrastructure-layer concerns that must not leak into Domain
or Application; the current placeholder `Worker` avoids introducing any messaging
dependency prematurely, consistent with "do not add Kafka implementation code yet."

## 9. Module registration extension naming

**Assumption:** `Add{Module}Application()` and
`Add{Module}Infrastructure(IConfiguration configuration)`, matching §49 of
`06-solution-structure.md` verbatim. Placed as static extension classes at the root
namespace of each Application / Infrastructure project. All extensions are
currently no-ops (return `services` unchanged) — no use cases or adapters are
registered yet.

**ADR Traceability:** ADR-0001 §7-§9 describes Application as the orchestration layer
composed by Hosts; the extension pattern keeps that composition explicit and
testable without requiring Infrastructure adapters to exist yet.

## 10. Architecture Tests project references

**Assumption:** `Yunu.Commerce.ArchitectureTests` directly references only each
module's **Infrastructure** project. Because Infrastructure transitively depends on
Domain, Application and Contracts, their assemblies are copied to the test output
directory and can be loaded by name (`Assembly.Load("Yunu.Commerce.{Module}.Domain")`)
without a direct compile-time reference, keeping the test project's reference list
minimal while still exercising every layer.

**ADR Traceability:** Operationalizes ADR-0001 §6/§13-§14 (Domain forbidden
dependencies, Bounded Context independence, no shared database integration) and
ADR-0002 §2 (Bounded Context ownership) as executable, CI-enforceable rules rather
than only documented conventions.

## 11. Integration Tests project

**Assumption:** `Yunu.Commerce.IntegrationTests` is created with a single
placeholder passing test and a reference to `Yunu.Commerce.Api` only. Testcontainers
and real infrastructure dependencies (§42 of `06-solution-structure.md`) are
intentionally **not** wired yet, since no Infrastructure adapters exist to test.

**ADR Traceability:** Deferred pending concrete adapters described in ADR-0003
(persistence), ADR-0004/0005 (Kafka/Outbox), ADR-0006 (Redis), and ADR-0007
(Elasticsearch). Testcontainers for these technologies will be introduced
alongside their respective Infrastructure implementations, not during the
architecture skeleton phase.

## 12. Central Package Management package list

**Assumption:** Only packages strictly required to compile the skeleton were added
to `Directory.Packages.props`:
`Microsoft.Extensions.DependencyInjection.Abstractions`,
`Microsoft.Extensions.Configuration.Abstractions`,
`Microsoft.Extensions.Hosting.Abstractions`,
`Microsoft.Extensions.Diagnostics.HealthChecks`,
`OpenTelemetry`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.Console`,
`Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `NetArchTest.Rules`.

No MongoDB, Kafka, Redis, Elasticsearch, Azure AI, or Google AI packages were added.
ASP.NET Core itself is framework-referenced (not a NuGet package) via
`Microsoft.NET.Sdk.Web`.

**ADR Traceability:** Directly honors the "not yet" deferral pattern established by
ADR-0003 (MongoDB.Driver / relational drivers deferred to Infrastructure), ADR-0004
(Confluent.Kafka deferred), ADR-0005 (Outbox persistence packages deferred),
ADR-0006 (StackExchange.Redis deferred), ADR-0007 (Elasticsearch client deferred),
ADR-0008 (Azure AI / Google AI SDKs deferred), and ADR-0009 (Azure SDKs deferred
from the core bootstrap; cloud-specific packages remain an Infrastructure/Host
deployment concern introduced only when a concrete adapter is implemented).

## 13. Solution Folder layout

**Assumption:** Visual Studio Solution Folders mirror the physical `src`/`tests`
layout exactly: `BuildingBlocks`, `Modules/{ModuleName}`, `Hosts`,
`Tests/Architecture`, `Tests/Unit`, `Tests/Integration` — matching §46 of
`06-solution-structure.md`.

**ADR Traceability:** Reflects ADR-0002 §2 (initial Bounded Context list) and the
"Search, AI and Integrations are supporting capabilities rather than owners of
canonical commerce entities" clarification in ADR-0002 §2, which is why those three
modules have no `Domain` solution sub-folder.

## 14. `.editorconfig` scope

**Assumption:** A general-purpose C#/.NET `.editorconfig` was generated (4-space
indentation, file-scoped namespaces, interface `I` prefix, async `Async` suffix).
No project-specific style guidance existed prior to this bootstrap, so a standard,
widely-accepted baseline was chosen per §39 of `06-solution-structure.md`.

**ADR Traceability:** Not addressed by any ADR; purely a code-style convention with
no bearing on dependency direction or Bounded Context isolation.

## 15. Assembly markers

**Assumption:** Domain and Contracts projects (which receive no other source file
during this phase) each get a single `AssemblyMarker.cs` containing an empty
`public sealed class AssemblyMarker`. This is not a business type — it exists solely
so the assembly compiles to something non-trivial and is discoverable by
`NetArchTest`/reflection, per the "allowed skeleton content" list in §48 of
`06-solution-structure.md`.

**ADR Traceability:** Consistent with ADR-0001 §5-§6, which permits Domain-only
technical scaffolding as long as no business Aggregate, Entity or Value Object is
fabricated. `AssemblyMarker` intentionally carries zero business meaning.

## 16. No `.gitignore` changes

Not requested in this task's file list; left untouched.

**ADR Traceability:** Not applicable.

---

# ADR Traceability Summary

| ADR | Title | Where Applied in Skeleton |
|---|---|---|
| **ADR-0001** | Use DDD, Clean Architecture, Hexagonal Architecture | Assumptions #1, #3, #4, #7, #8, #9, #10, #15. Enforced by `DependencyRuleTests` (Domain↛Application, Domain↛Infrastructure, Application↛Infrastructure, forbidden vendor namespaces in Domain/Application). Hosts (`Yunu.Commerce.Api`, `Yunu.Commerce.Worker`) act strictly as composition roots with no business logic. |
| **ADR-0002** | Bounded Context Strategy | Assumptions #4, #13. Module list (Catalog, Sellers, Offers, Pricing, Availability, Fulfillment, Freight) matches ADR-0002 §2 exactly. Search, AI, Integrations correctly excluded from having a `Domain` project, per ADR-0002 §2's classification of them as supporting capabilities rather than canonical-entity owners. |
| **ADR-0003** | Database per Bounded Context | Assumption #12. No MongoDB.Driver, EF Core, or relational driver packages added to any project; persistence technology selection (MongoDB for Catalog/Availability, relational for Sellers/Offers/Pricing/Fulfillment per ADR-0003 §9-§13) is deferred entirely to future Infrastructure implementation, not introduced during scaffolding. |
| **ADR-0004** | Use Kafka for Event-Driven Integration | Assumptions #4, #8, #12. No `Confluent.Kafka` package referenced anywhere. `Yunu.Commerce.EventBus` contains only the `IIntegrationEventPublisher` abstraction (messaging port), with no broker-specific implementation. Enforced by architecture test forbidden-namespace checks. |
| **ADR-0005** | Use Transactional Outbox | Assumptions #8, #11. No Outbox persistence schema or publisher is scaffolded yet; `Worker`'s placeholder `BackgroundService` explicitly avoids simulating Outbox processing until a concrete Infrastructure adapter exists per a module's persistence choice. |
| **ADR-0006** | Use Redis for Distributed Cache | Assumptions #5, #12. No `StackExchange.Redis` package added; no cache port implementation scaffolded. Health checks omit Redis-dependent checks since no cache adapter exists yet. |
| **ADR-0007** | Use Elasticsearch for Search Projections | Assumptions #5, #12, #13. `Yunu.Commerce.Search.Infrastructure` project created with no Elasticsearch client package; `Search` module correctly has no `Domain` project (search documents are derived projections, not canonical business state per ADR-0007 §3). |
| **ADR-0008** | GenAI Provider Abstraction | Assumptions #4, #12, #13. `Yunu.Commerce.AI.Application`/`Contracts`/`Infrastructure` scaffolded with no Azure AI or Google AI SDK package added; no `Domain` project created for AI, consistent with AI orchestration being provider-neutral capability composition rather than a Bounded Context owning canonical commerce Aggregates. Architecture test `AI_Application_Should_Not_Depend_On_Cloud_AI_Providers` enforces this going forward. |
| **ADR-0009** | Cloud Provider Strategy (Azure-first, not Azure-locked) | Assumptions #6, #12. No Azure SDK package referenced in any Domain, Application, or BuildingBlocks project. OpenTelemetry bootstrap uses a vendor-neutral console exporter rather than Azure Monitor/Application Insights, preserving cloud portability until a concrete deployment decision is made at the Infrastructure/Host level. |

---

**Conclusion of ADR Traceability Review:** No inconsistency was found between the
already-approved project tree, project-reference graph, or
`tools/bootstrap-solution.ps1` and any of the nine accepted ADRs. The script is
**not modified** as a result of this review.