# Architecture Decisions

This is a concise record of the decisions that shape the current repository. It is not a claim that every alternative is universally inferior.

## Modular monolith rather than microservices

**Context:** The project needs clear business and infrastructure boundaries, but its current scope does not require independent deployment or network ownership.

**Decision:** Keep API, Application, Domain, and Infrastructure as explicit projects in one deployable application.

**Rationale:** Reliability patterns can be demonstrated without adding artificial service discovery, remote calls, and distributed operational failure modes.

**Trade-off:** Modules cannot be scaled or deployed independently; a future split would require explicit contracts and operational ownership.

## Transactional Outbox rather than database/broker dual-write

**Context:** Saving payment state and publishing directly to RabbitMQ are two separate writes that cannot commit atomically.

**Decision:** Persist business state and the derived Outbox message in one SQL transaction, then publish asynchronously.

**Rationale:** A committed business transition always has durable event intent even when RabbitMQ is unavailable.

**Trade-off:** Publication is delayed and at least once; the dispatcher and retention of accumulated rows require operational care.

## Application-managed Product version for inventory concurrency

**Context:** Concurrent order requests can load the same stock quantity and otherwise overwrite each other's changes.

**Decision:** Use an application-managed Product version as an EF Core concurrency token.

**Rationale:** The losing update becomes an explicit concurrency conflict instead of silently overselling or losing a stock mutation.

**Trade-off:** Callers must handle retryable conflicts, and every relevant Product mutation must advance the version correctly.

## Payment.Status as the terminal-transition concurrency token

**Context:** The current Payment lifecycle has one important race: multiple requests attempting `Pending` to terminal state.

**Decision:** Use the original `Payment.Status` value as the EF Core concurrency token for the terminal update.

**Rationale:** The business transition itself supplies the concurrency change, so a separate Version column is unnecessary at the current lifecycle complexity.

**Trade-off:** A more complex Payment state machine or non-status concurrent edits may eventually justify a separate version token.

## Dedicated DbMigrator rather than API-startup migrations

**Context:** Applying schema changes inside every API process couples runtime startup to schema deployment and can create multi-instance migration races.

**Decision:** Use `EcommerceTxPr.DbMigrator` as a separate executable and make the Compose API depend on its successful completion.

**Rationale:** Schema deployment and application runtime have distinct responsibilities and failure reporting.

**Trade-off:** Deployments must run and observe an additional process before starting the API.

## Provider idempotency rather than a distributed transaction

**Context:** SQL Server cannot atomically commit with an external payment provider through the normal application transaction.

**Decision:** Commit a durable `Pending` Payment first, then call the provider with a stable key derived from the Payment identity.

**Rationale:** A later equivalent request can reuse the same provider identity after a lost response or failed final local save. Provider idempotency protects the external effect, while `Payment.Status` concurrency separately protects local terminal persistence.

**Trade-off:** Correctness depends on the provider honoring equivalent idempotent replay, and unresolved `Pending` Payments need a later recovery trigger.

## Inbox deduplication rather than an exactly-once claim

**Context:** Confirmed publication, network failures, and manual acknowledgements can all produce duplicate delivery.

**Decision:** Persist a stable `MessageId` in the Inbox and commit it with `PaymentEventProjection` in one local save.

**Rationale:** A matching repeated delivery becomes harmless and can be acknowledged without repeating the projection effect.

**Trade-off:** Delivery remains at least once, each consumer effect needs its own idempotency design, and Inbox retention is not yet automated.

## RabbitMQ excluded from readiness-critical startup

**Context:** The transactional Outbox is intended to separate local business availability from temporary broker availability.

**Decision:** Validate RabbitMQ configuration at startup, but establish connections and topology lazily; SQL determines readiness while root health reports RabbitMQ degradation.

**Rationale:** The API can start and commit database-backed work while Outbox messages accumulate for later publication.

**Trade-off:** Broker-dependent propagation is delayed, and operators must monitor root dependency health and Outbox backlog separately.

## Explicit versioned Outbox payloads

**Context:** Internal Domain Event types can evolve for application reasons that should not silently change an existing integration contract.

**Decision:** Map known events to explicit `PaymentSucceededV1Payload` and `PaymentFailedV1Payload` contracts with stable v1 message types.

**Rationale:** `payment.succeeded.v1` and `payment.failed.v1` remain deliberate external representations instead of blind serialization of internal event objects.

**Trade-off:** Every new event or contract version needs explicit mapping, serialization, and tests; unknown event types intentionally fail closed.

## Simulated Development payment gateway

**Context:** The project needs deterministic success, failure, and recovery behavior without implying a real provider integration.

**Decision:** Register a clearly named simulated adapter in Development and replace `IPaymentGateway` with explicit deterministic doubles in integration tests.

**Rationale:** Outcomes come from validated configuration or the test double, never from amount, Order ID, client input, randomness, or magic request strings.

**Trade-off:** The adapter proves the application boundary and recovery orchestration, not production provider authentication, webhooks, reconciliation, or compliance.

## SQLite integration tests plus real Compose smoke

**Context:** Ordinary integration tests need to be fast and deterministic, while SQLite cannot reproduce every SQL Server or RabbitMQ behavior.

**Decision:** Use SQLite with `WebApplicationFactory` for isolated integration tests and validate the real SQL Server/RabbitMQ path in the Compose smoke job.

**Rationale:** Most behavior receives rapid feedback, while migrations, SQL persistence, broker topology, health, and end-to-end container startup still run against the actual infrastructure classes.

**Trade-off:** Provider-specific behavior needs targeted classifiers/tests and the slower smoke job; SQLite success alone is not treated as production-path proof.
