# Payment Reliability

The `Payment` entity is the durable payment intent. The application does not call the gateway and then attempt to remember what happened; it first commits a `Pending` Payment and uses that persisted identity for every equivalent provider attempt.

## The Failure Window

SQL Server cannot atomically commit with an external payment provider in the application's normal transaction. A provider can perform a charge while the API loses the response, or the provider can return success while the final local database save fails.

Calling the provider with a new identity on retry could repeat the external effect. Calling it before any durable local record exists would also leave the application without a stable recovery point. EcommerceTxPr addresses those risks with this ordering:

```text
create Payment(Pending)
-> COMMIT #1
-> derive provider idempotency key from PaymentId
-> call gateway
-> apply a terminal domain transition
-> COMMIT terminal local state
```

If the first commit fails, the gateway is not called. If a later step fails or is uncertain, the persisted `Pending` Payment remains available for recovery.

## Durable Intent and Recovery Sequence

The most important recovery path is provider success followed by failure of the local terminal save:

```mermaid
sequenceDiagram
    autonumber
    actor Client
    participant API
    participant SQL as SQL Server
    participant Provider as Payment Provider Boundary

    Client->>API: POST /api/orders/{orderId}/payments
    API->>SQL: INSERT Payment(Pending)
    SQL-->>API: COMMIT #1 succeeds
    API->>Provider: Process(PaymentId, Amount, stable key)
    Provider-->>API: Succeeded(provider reference)
    API->>SQL: Payment Succeeded + Order Paid + derived Outbox row
    SQL--xAPI: Final local commit fails
    API--xClient: Request fails; Pending intent remains

    Client->>API: Retry the same payment POST
    API->>SQL: Load the same Pending Payment
    SQL-->>API: Same PaymentId and Amount
    API->>Provider: Process with the same stable key
    Provider-->>API: Replay the original Success
    Note over API,Provider: Gateway requests: 2; provider effect executions: 1
    API->>SQL: Payment Succeeded + Order Paid + derived Outbox row
    SQL-->>API: One successful terminal COMMIT
    API-->>Client: 200 OK (resumed processing)
```

The retry is safe only because it reuses the same `PaymentId`, amount, and provider idempotency key. The deterministic test gateway models the expected provider contract: the second equivalent request is still a gateway request, but it replays the exact stored result rather than executing a second external effect. Reusing the same key with a different Payment ID or amount fails closed.

The Outbox row in the diagram is derived from an in-memory Payment Domain Event. The event supplies the business occurrence time and mapping data; the explicit versioned Outbox representation is the durable database row. The Domain Event is not committed as an independent record.

## Why Two Idempotency Mechanisms Are Needed

Provider idempotency and EF Core optimistic concurrency protect different boundaries:

```text
Stable provider key derived from PaymentId
-> prevents repeated equivalent gateway requests from repeating the external effect

Payment.Status concurrency token
-> prevents two local Pending-to-terminal updates from both committing
```

The business transition itself changes the concurrency token from `Pending` to `Succeeded` or `Failed`. EF Core includes the original `Pending` value in the terminal update condition. If another request already persisted a terminal status, the losing update affects no row and becomes a concurrency conflict.

After that known conflict, the unit of work clears the losing change tracker. The service reloads both Payment and Order instead of trusting the losing in-memory objects. It accepts only these persisted pairs:

| Persisted Payment | Persisted Order | Interpretation |
| --- | --- | --- |
| `Succeeded` | `Paid` | Replay the successful terminal result |
| `Failed` | `Pending` | Replay the failed terminal result |
| `Pending` | `Pending` | Another terminal operation did not commit; return the concurrent-processing conflict |

Any other pair fails closed as an invariant violation. In particular, a locally `Succeeded` Payment with a `Pending` Order would contradict the required atomic terminal transaction.

## Indeterminate Outcomes

A gateway result can be indeterminate: the application cannot safely call it success or failure. This remains a non-terminal state:

```text
HTTP 503 Service Unavailable
Payment remains Pending
Order remains Pending
no terminal Payment Domain Event is raised
no terminal Outbox row is persisted
```

The Problem Details code is `Payment.OutcomeIndeterminate`. A later `POST /api/orders/{orderId}/payments` loads the same `Pending` Payment, derives the same provider key, and asks the gateway again. The client does not supply the Payment ID, amount, key, provider result, or simulated outcome.

An unexpected final database failure is deliberately not wrapped in a generic automatic retry. Retrying the local save with stale tracked state could hide an invariant or repeat unsafe orchestration. The request fails, the committed `Pending` Payment remains the recovery point, and a later request resumes through the normal service path.

## Local Atomicity

Once the gateway returns a terminal result, the application performs one final local save:

```text
Payment terminal state
+ Order state
+ Outbox message derived from the in-memory Domain Event
= one local SQL transaction
```

For success, this means Payment `Succeeded`, Order `Paid`, and a `payment.succeeded.v1` Outbox row. For a provider-declared failure, it means Payment `Failed`, Order `Pending`, and a `payment.failed.v1` Outbox row.

If the final save loses an optimistic-concurrency race, none of the losing Payment mutation, Order mutation, or derived Outbox row commits. The persisted winner is reloaded and reconciled through the valid-pair rules above.

## Boundaries

- There is no distributed transaction between SQL Server and the payment provider.
- Provider idempotency depends on the provider honoring exact replay for an equivalent Payment ID and amount.
- `Payment.Status` concurrency protects local terminal persistence; it cannot prevent an external effect.
- The checked-in gateway is deterministic simulated Development infrastructure, not a production payment provider.
- There is no automatic reconciliation worker for `Pending` Payments; the current recovery trigger is a client retry of the payment endpoint.
- There are no provider webhooks, multiple payment methods, or refund workflows in the current scope.
