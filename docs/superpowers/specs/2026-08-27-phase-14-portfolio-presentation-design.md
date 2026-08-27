# Phase 14: Portfolio Presentation and Final Repository Review

**Date:** 2026-08-27

**Status:** Approved design, pending implementation plan

**Scope:** Documentation, presentation, repository hygiene, and public-release readiness

## 1. Purpose

Phase 14 turns the completed reliability-focused backend into a portfolio repository that can be understood at three depths:

- a recruiter should identify the project, stack, and engineering value in about 30 seconds;
- a backend engineer should understand the principal consistency and recovery mechanisms in about three minutes;
- a repository evaluator should be able to run the system, follow a real API flow, inspect the architecture, find the tests, and understand its trade-offs in about ten minutes.

The phase presents the current system accurately. It does not add architecture, infrastructure, business behavior, or reliability mechanisms.

## 2. Boundaries

### In scope

- replace the obsolete `README.md` with a professional English portfolio landing page;
- add focused architecture documentation under `docs/architecture/`;
- correct the existing MIT license copyright placeholder;
- remove only tracked artifacts or references proven obsolete;
- validate all commands, links, claims, and API examples against the repository;
- audit the relationship between the completed branch and the public/default branch;
- provide a safe fast-forward publication procedure if remote mutation is not performed;
- recommend concise GitHub repository metadata.

### Out of scope

- application behavior changes;
- new business features or endpoints;
- new infrastructure, messaging, persistence, security, deployment, or observability technologies;
- RabbitMQ consumer or publisher redesign;
- payment-provider behavior changes;
- branch history rewriting, force-pushing, or working-branch deletion;
- claims of exactly-once messaging, production readiness, PCI compliance, high availability, or a microservices architecture.

If the documentation audit reveals a genuine production bug, implementation stops and the defect is reported rather than silently corrected in this phase.

## 3. Documentation strategy

The repository will use a layered portfolio narrative:

1. A strong, skimmable README acts as the public landing page.
2. A compact README diagram communicates the system shape without dominating the first half of the page.
3. Focused architecture documents hold the detailed system, payment-recovery, messaging, and decision explanations.

The README target is approximately 2,000–2,500 words. The modernization journey will be short and problem-oriented; the current architecture and its guarantees remain the primary subject.

## 4. README information architecture

The README will be replaced completely and organized as follows:

1. **Title and positioning** — `EcommerceTxPr` described as a reliability-focused .NET 8 e-commerce backend and architecture portfolio project, not a commercial platform.
2. **Truthful badges** — the actual GitHub Actions workflow, .NET 8, and MIT license. The CI badge will use `.github/workflows/ci.yml` and avoid a stale branch-qualified status where possible.
3. **Why this project exists** — a concise explanation that the repository demonstrates how ordinary business operations can remain recoverable across duplicate HTTP requests, concurrent writes, payment uncertainty, database/broker separation, and duplicate message delivery.
4. **Key engineering highlights** — HTTP idempotency, optimistic concurrency, durable payment intent, provider idempotency, transactional persistence of business state plus the Outbox row derived from an in-memory Domain Event, at-least-once RabbitMQ publication, and Inbox-based consumer idempotency.
5. **Reliability guarantees table** — each failure mode paired with the mechanism that addresses it and the exact boundary of that guarantee.
6. **Compact architecture view** — a small Mermaid diagram showing Client, API/modules, SQL Server, simulated payment gateway, Outbox dispatcher, RabbitMQ, consumer, Inbox, and projection at component level.
7. **Quick Start** — Docker/Compose-only prerequisites, platform-specific `.env.example` copy commands, `docker compose up --build`, verified service URLs, and shutdown guidance.
8. **Startup and health behavior** — migrator ordering, RabbitMQ worker isolation, and the exact liveness/readiness/dependency semantics.
9. **Executable API walkthrough** — customer, stocked product, first order, order replay, payment, payment lookup, and order lookup using verified controllers and DTOs.
10. **Focused idempotency and payment retry explanations** — exact HTTP outcomes, canonical request equivalence, durable `Pending` payment behavior, and client/provider trust boundaries.
11. **Architecture links and project structure** — short descriptions of the seven principal projects and links to all four architecture documents.
12. **Technology, testing, and CI** — only technologies and validations actually present in the repository.
13. **Design boundaries, limitations, future work, and license** — honest scope limits, a short future-work list, and a relative link to `LICENSE.txt`.

The first screen will communicate the project type, stack, strongest reliability mechanisms, and verified quality baseline without beginning with setup instructions or a large diagram.

## 5. Architecture documents

### 5.1 `docs/architecture/system-overview.md`

This document will provide the detailed component view omitted from the README. Its Mermaid diagram and surrounding text will show:

- Client to ASP.NET Core API;
- API, Application, Domain, and Infrastructure responsibilities within one modular monolith;
- EF Core and SQL Server as the local transactional boundary;
- the explicitly simulated development payment gateway as an external-provider boundary concept;
- in-memory Domain Event mapped to a durable transactional Outbox message, then dispatched to RabbitMQ;
- RabbitMQ to payment-event consumer to Inbox and `PaymentEventProjection`;
- the dedicated `EcommerceTxPr.DbMigrator` startup role;
- the distinction between synchronous request processing and asynchronous event publication/consumption.

It will state that these are code/module boundaries in one deployable application, not separately deployed microservices.

### 5.2 `docs/architecture/payment-reliability.md`

This document will contain the principal payment recovery sequence diagram:

1. The client posts a payment request for an order.
2. The API creates and commits a durable `Pending` Payment.
3. The application derives a stable provider idempotency key from the persisted Payment identity.
4. The gateway is invoked.
5. The provider performs one successful effect.
6. The final local terminal save fails.
7. The request fails, but the durable `Pending` Payment remains the recovery point.
8. A later client retry loads the same Payment and uses the same provider key.
9. The provider replays the original successful result.
10. Payment `Succeeded`, Order `Paid`, and the Outbox message derived from the in-memory Domain Event commit locally in one SQL transaction.

The diagram will explicitly annotate **two gateway requests and one provider-side effect**. It will also cover the indeterminate outcome: HTTP `503`, Payment remains `Pending`, Order remains `Pending`, and no terminal payment Domain Event or Outbox message is created.

The document will state that SQL Server and the payment provider do not participate in a distributed transaction. Provider idempotency protects the external effect; the `Payment.Status` concurrency token protects the local terminal transition; neither replaces the other.

### 5.3 `docs/architecture/messaging-reliability.md`

This document will explain the messaging lifecycle in three compact flows:

- **Producer transaction:** terminal Payment state + Order state + the Outbox message derived from the in-memory Domain Event commit in one SQL transaction.
- **Publication:** dispatcher loads pending Outbox messages, establishes RabbitMQ lazily, publishes with mandatory routing and publisher confirms, and marks a message processed only after confirmed publication.
- **Consumption:** consumer receives with `autoAck: false` and prefetch one, copies the delivery body, applies Inbox deduplication and the projection in one database save, then acknowledges only after processing succeeds.

The duplicate path will show the same `MessageId` finding an existing Inbox row, producing no repeated projection effect, and being acknowledged. A mismatched message type for an existing identity will be described as poison/inconsistent identity rather than a duplicate.

The document will state prominently: the system provides **at-least-once delivery with idempotent consumer processing, not exactly-once messaging**. A publication may reach RabbitMQ before the local Outbox processed marker is committed, so duplicate publication and delivery remain valid outcomes.

### 5.4 `docs/architecture/decisions.md`

This will be a concise decision log rather than a directory of formal ADRs. Each entry will use four short fields:

- **Context** — the problem being addressed;
- **Decision** — the chosen design;
- **Rationale** — why it fits this repository;
- **Trade-off** — what remains limited or more complex.

The decisions will cover:

- modular monolith rather than artificial microservices;
- transactional Outbox rather than database/broker dual-write;
- Inbox deduplication rather than an exactly-once claim;
- provider idempotency rather than a distributed transaction;
- `Payment.Status` as the terminal-transition concurrency token;
- application-managed Product versioning for inventory concurrency;
- SQLite integration tests plus real SQL Server/RabbitMQ Compose smoke;
- a dedicated migrator rather than automatic API-startup migrations;
- RabbitMQ excluded from readiness-critical startup because the Outbox tolerates temporary broker unavailability;
- an explicitly simulated Development gateway rather than presenting a fake production integration.

## 6. Technical claims contract

Portfolio documentation will preserve these exact boundaries:

| Claim | Accurate scope |
| --- | --- |
| Modular monolith | One application with explicit API, Application, Domain, and Infrastructure project boundaries; not microservices. |
| HTTP idempotency | Order creation requires one `Idempotency-Key` header. The key value is treated as an exact, case-sensitive value, stored through its hash, and associated with the canonical logical request fingerprint. An equivalent logical request replays; different logical reuse conflicts. |
| Optimistic concurrency | Product inventory uses an application-managed version token; Payment uses its `Status` transition from `Pending` as the terminal-write concurrency token. |
| Durable payment intent | A `Pending` Payment is committed before the gateway call so a later request can resume using the same identity after an uncertain or failed observation. |
| Provider-idempotent retry | The stable key derived from Payment identity allows equivalent retries to replay the stored provider result; it does not make the local database transition atomic with the provider. |
| Transactional Outbox | Payment terminal state, Order state, and the Outbox message derived from the in-memory Domain Event commit in the same local SQL transaction. The Domain Event is the mapping source; the Outbox representation is the durable row. |
| Broker isolation | Valid RabbitMQ configuration with a temporarily unavailable broker does not prevent API startup; pending Outbox rows remain the source of retry work. |
| At-least-once messaging | Publisher confirms and manual acknowledgements improve reliability but allow duplicate publication or delivery. |
| Idempotent consumer | Inbox identity and projection changes commit atomically so a repeated delivery does not repeat the projection effect. |
| Simulated provider | The configured Development adapter is deterministic test/development infrastructure, not a production payment provider. |

Documentation will reject or rewrite the terms `exactly-once`, `production-ready`, `enterprise-grade`, `PCI compliant`, `high availability`, `secure payment processing`, `distributed transaction`, and `microservices architecture` unless used explicitly to say the project does not claim them.

## 7. Verified API walkthrough contract

Every example will be derived from the current controllers, DTOs, and integration tests. ASP.NET Core's web defaults expose the record properties as camel-case JSON.

| Step | Verified request | Required input | Success behavior | Response body |
| --- | --- | --- | --- | --- |
| Create Customer | `POST /api/customers` | JSON `name`, `birthDate` | `201 Created`; `Location` targets `GET /api/customers/{id}` | `id`, `name`, `birthDate`, `creationDate` |
| Create Product | `POST /api/products` | JSON `sku`, `name`, `price`, `stockQuantity` | `201 Created`; `Location` targets `GET /api/products/{id}` | `id`, `sku`, `name`, `price`, `stockQuantity`, `creationDate` |
| Create Order | `POST /api/orders` | one `Idempotency-Key` header; JSON `customerId`, non-empty `items`; each item has `productId`, positive `quantity` | first creation `201 Created`; `Location` targets `GET /api/orders/{id}` | `id`, `customerId`, `status`, `creationDate`, `total`, `items`; item fields are `productId`, `productName`, `unitPrice`, `quantity`, `lineTotal` |
| Replay Order | repeat `POST /api/orders` with the same key and equivalent logical request | same logical customer/items | `200 OK` with the persisted Order response | same Order identity and persisted representation |
| Conflicting Order reuse | repeat `POST /api/orders` with the same key but a different logical request | changed customer, product, or aggregate quantity | `409 Conflict` with code `Order.IdempotencyKeyConflict` | RFC 7807-style Problem Details with `code` extension |
| Process Payment | `POST /api/orders/{orderId}/payments` | no request body and no provider-control input | first completed processing `201 Created`; resumed or terminal replay `200 OK`; indeterminate provider outcome `503 Service Unavailable` | Payment fields `id`, `orderId`, `amount`, `status`, `creationDate`, `providerReference`, `failureCode` |
| Retrieve Payment | `GET /api/orders/{orderId}/payment` | route Order ID | `200 OK`, or `404` when absent | Payment response |
| Retrieve Order | `GET /api/orders/{id}` | route Order ID | `200 OK`, or `404` when absent | Order response, allowing the walkthrough to show `Paid` after successful payment |

The primary walkthrough will use Unix-style `curl` commands and `jq` only to capture server-generated identifiers and format output. Quick Start itself will require only Docker and Docker Compose. A note will state that `jq` is a shell convenience, not an application or container dependency. Where clarity improves, commands may show `<CUSTOMER_ID>`, `<PRODUCT_ID>`, and `<ORDER_ID>` alternatives.

The order body will be reused exactly for the first request and replay demonstration. A focused variation will then reuse the same key with a changed quantity to demonstrate `409 Conflict`. The explanation will additionally state the implemented logical equivalence rule: repeated product items are aggregated and items are ordered canonically, so reordered items or split quantities with the same totals replay rather than conflict.

The walkthrough will not let the client supply Payment ID, amount, provider idempotency key, simulated gateway outcome, provider reference, or failure code. `POST /api/orders/{orderId}/payments` has no request body; these values are determined behind the API boundary.

## 8. Quick Start and operational presentation

Quick Start prerequisites will be Docker Engine or Docker Desktop with Docker Compose. It will show:

```bash
cp .env.example .env
docker compose up --build
```

and the PowerShell equivalent:

```powershell
Copy-Item .env.example .env
docker compose up --build
```

The documented URLs, verified from `compose.yml`, `.env.example`, and `Program.cs`, will be:

- API: `http://localhost:8080`
- Swagger in the configured Development environment: `http://localhost:8080/swagger`
- full dependency health: `http://localhost:8080/health`
- liveness: `http://localhost:8080/health/live`
- readiness: `http://localhost:8080/health/ready`
- RabbitMQ Management using `.env.example`: `http://localhost:15673`

Startup behavior will be described precisely:

1. SQL Server starts and becomes healthy.
2. `EcommerceTxPr.DbMigrator` applies EF Core migrations.
3. The API starts only after the migrator exits successfully.
4. RabbitMQ runs independently of that SQL/migrator dependency chain.
5. RabbitMQ workers establish connections lazily and retry on their configured polling/reconnect cycles.

Health semantics will remain unchanged:

- `/health/live` checks the application process;
- `/health/ready` checks the primary SQL database;
- `/health` includes SQL and RabbitMQ;
- RabbitMQ unavailable makes root health `Degraded` with HTTP `200`;
- SQL unavailable makes the relevant health result `Unhealthy` with HTTP `503`.

The documentation will say that the Outbox allows local business transactions to remain durable during temporary broker unavailability. It will not claim that every feature is unaffected indefinitely.

## 9. Testing and CI presentation

The README will report only the freshly verified baseline:

- 136 unit tests passed;
- 187 integration tests passed;
- 323 total passed;
- zero failed and zero skipped;
- Release build with zero warnings and zero errors.

The testing explanation will cover domain invariants, application services, API semantics, order idempotency, optimistic concurrency, payment recovery, Outbox atomicity, RabbitMQ publisher behavior, consumer lifecycle/manual acknowledgements, Inbox idempotency, health endpoints, SQLite persistence, and the real SQL Server/RabbitMQ path exercised by Compose smoke. It will not claim a coverage percentage.

The CI section will describe the two actual jobs in `.github/workflows/ci.yml`:

- **Quality:** local tool restore, package restore, Release build with warnings as errors, tests, EF migration drift check, and vulnerable-package audit.
- **Production smoke:** Compose validation, API and migrator image builds, SQL Server and RabbitMQ startup, migrations, health checks, RabbitMQ topology verification, and SQL-backed API persistence.

## 10. Project structure and technology

The README will list only the main projects, each with one sentence:

- `EcommerceApi.V2` — HTTP controllers, error handling, health endpoints, and process composition.
- `EcommerceTxPr.Application` — use cases, ports, contracts, and application-level reliability orchestration.
- `EcommerceTxPr.Domain` — business entities, invariants, statuses, and Payment domain events.
- `EcommerceTxPr.Infrastructure` — EF Core persistence, migrations, Outbox/Inbox mapping, RabbitMQ adapters/workers, and the simulated Development gateway.
- `EcommerceTxPr.DbMigrator` — dedicated executable for applying EF Core migrations before API startup.
- `EcommerceTxPr.UnitTests` — fast domain and application tests using handwritten deterministic doubles.
- `EcommerceTxPr.IntegrationTests` — API, persistence, concurrency, messaging, consumer lifecycle, and reliability integration tests.

The technology list will be limited to what is present: .NET 8, ASP.NET Core, C#, EF Core 8, SQL Server, SQLite for isolated integration tests, RabbitMQ.Client, RabbitMQ, Docker, Docker Compose, GitHub Actions, xUnit, `WebApplicationFactory`, and handwritten deterministic test doubles.

## 11. Limitations and future work

The README will present these as deliberate current scope boundaries:

- no authentication or authorization;
- deterministic simulated payment provider only;
- no provider webhooks or automated `Pending` Payment reconciliation worker;
- one Payment operation per Order, with no multiple payment methods or refunds;
- no retry exchange, delayed-message plugin, or dead-letter queue topology;
- no Inbox/Outbox retention cleanup;
- no production observability platform or cloud deployment;
- single-node local RabbitMQ and SQL Server Developer edition in the local environment.

Future work will remain short: a real provider adapter and webhook/reconciliation model, broker retry/DLQ policy, retention jobs, authentication/authorization, production telemetry, and deployment to a real target.

## 12. License and repository hygiene

`LICENSE.txt` will retain the existing MIT text byte-for-byte apart from replacing the placeholder line with:

```text
Copyright (c) 2024 Leonardo Fernandes Contrera
```

The README will link to the actual file as `[MIT License](LICENSE.txt)`.

Repository hygiene will inspect tracked content for `.env`, credentials, keys, `bin`, `obj`, `TestResults`, patch files, smoke-test artifacts, obsolete `EcommerceTxPr.Aplication` spelling, `DatabaseInfo`, the old connection-string instructions, and the obsolete "simple API" description. Files will be removed only when proven obsolete. The license will not be removed.

All relative documentation links, the workflow badge target, and the license link will be checked manually and by repository search. No Markdown tool will be added solely for this phase.

## 13. Public repository visibility gate

The audited state before documentation work is:

- completed implementation branch: `refactor/foundation`;
- remote implementation branch: `origin/refactor/foundation` at the same commit;
- public/default remote branch: `origin/master`;
- `origin/refactor/foundation` is 20 commits ahead and zero behind `origin/master`;
- `origin/master` is an ancestor of the modern branch;
- the working tree is clean;
- `origin/master` still exposes the obsolete portfolio landing page.

Repository implementation complete is not equivalent to public portfolio presentation complete. Phase 14 cannot be reported as publicly complete while GitHub opens the obsolete `master` state.

After the final documentation commit is pushed to `refactor/foundation`, the relationship will be re-audited. If `origin/master` remains an ancestor and the modern branch is zero behind, the safest procedure avoids the stale local `master` branch and performs a direct normal fast-forward:

```bash
git fetch --prune origin
git merge-base --is-ancestor origin/master refactor/foundation
git rev-list --left-right --count origin/master...refactor/foundation
git push origin refactor/foundation:master
git fetch origin master refactor/foundation
git rev-parse origin/master
git rev-parse origin/refactor/foundation
```

For this direct publication path, the ancestry command must exit zero, the divergence check must show `0` commits unique to `origin/master`, and the two final remote commit IDs must match. This is a fast-forward push, not a force-push. `refactor/foundation` will not be deleted.

If branch protection requires a pull request, the safe alternative is to push the modern branch and open a PR from `refactor/foundation` to `master`. Choose **Create a merge commit** when repository policy allows it. This preserves the existing modernization commits and adds a normal PR merge commit on `master`; do not squash or rebase merely to make history look cleaner.

After a PR merge-commit, equal branch-tip SHAs are neither expected nor required. Fetch both branches and verify instead that the complete feature tip is reachable from the default branch:

```bash
git fetch origin master refactor/foundation
git merge-base --is-ancestor origin/refactor/foundation origin/master
```

The ancestry command must exit zero, and the modern repository contents must be visible on the `master` landing page. Because `master` is already the default branch, no rename or default-branch change is needed after it receives the modern implementation.

The final manual GitHub check must open the repository landing page and visibly confirm:

- the new README;
- `Dockerfile` and `compose.yml`;
- unit and integration test projects;
- Infrastructure migrations;
- `EcommerceTxPr.DbMigrator`;
- `.github/workflows/ci.yml` / GitHub Actions;
- `docs/architecture/`.

If remote authentication, protection rules, or lack of authorization prevents publication, the final report will state clearly that implementation is complete but public portfolio presentation remains incomplete, then provide the exact remaining commands or GitHub actions. It will not claim the release gate passed.

## 14. GitHub metadata recommendation

If repository metadata cannot be updated from the environment, the final report will recommend:

**Description**

> Reliability-focused .NET backend with idempotent payments, optimistic concurrency, Outbox/Inbox messaging, RabbitMQ, SQL Server, Docker, and CI.

**Topics**

`dotnet`, `aspnet-core`, `csharp`, `backend`, `sql-server`, `entity-framework-core`, `rabbitmq`, `docker`, `distributed-systems`, `outbox-pattern`, `idempotency`, `optimistic-concurrency`

## 15. Final validation design

After documentation, license, and any proven hygiene corrections, run:

```text
git diff --check
dotnet restore
dotnet tool restore
dotnet build --configuration Release --no-restore -warnaserror
dotnet test --configuration Release --no-build
dotnet ef migrations has-pending-model-changes \
  --project EcommerceTxPr.Infrastructure \
  --startup-project EcommerceApi.V2 \
  --context EcommerceTxPrDbContext \
  --configuration Release \
  --no-build \
  -- \
  --ConnectionStrings:DefaultConnection="Server=localhost;Database=EcommerceTxPrDesignTime;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=1"
docker compose --env-file .env.example config --quiet
git status
```

Expected results are zero build errors and warnings, 136 unit tests passed, 187 integration tests passed, 323 total passed, zero failed, zero skipped, no pending EF model changes, and valid Compose configuration.

Because this phase is not designed to modify Docker or Compose behavior, the expensive full Compose smoke test will not be repeated unless those files change or another validation result creates a reason to rerun it. Any changed code or deployment behavior would require validation proportional to that change and may indicate that the documentation-only phase boundary has been crossed.

The final review will also verify:

- README word count and first-screen clarity;
- every curl method, route, JSON field, required input, status, and response field against current code;
- all relative README/document links;
- the badge references the real workflow;
- prohibited reliability/production claims are absent or explicitly negated;
- no unintended generated, secret, credential, patch, or environment files are tracked;
- the license diff changes only the approved copyright line;
- branch ancestry, divergence, push state, and public landing-page status.

## 16. Final report contract

The Phase 14 report will use these exact sections:

- **Baseline**
- **Public Repository Audit**
- **README**
- **Architecture Documentation**
- **Reliability Narrative**
- **Quick Start**
- **API Examples**
- **Repository Hygiene**
- **GitHub Metadata**
- **Final Validation**
- **Manual GitHub Steps**
- **Final Assessment**

It will report exact errors, warnings, unit, integration, total, failed, skipped, migration drift, and Compose configuration results. It will distinguish local repository completion from public portfolio visibility and will stop after Phase 14. There is no Phase 15.
