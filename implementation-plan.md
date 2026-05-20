# Implementation Plan — Suspend Claim for Fraud Suspicion

> **User Story:** As a claims manager, I want to be able to suspend a claim (Status: Suspended) if fraud is suspected. This suspension must automatically block the execution of any associated pending payments.

> ⚠️ **Constraint:** No C# code in this document. This is a pure architectural and design plan.

---

## Table of Contents

1. [Domain Layer](#1-domain-layer)
2. [Application Layer](#2-application-layer)
3. [Infrastructure & Tests](#3-infrastructure--tests)

---

## 1. Domain Layer

### 1.1 `ClaimStatus` Enumeration — Required Modifications

The enumeration must be extended with a new member to represent the suspended state:

| Member | Meaning |
|---|---|
| `Draft` | Claim has been created but not yet submitted. |
| `Submitted` | Claim has been submitted and is under review. |
| `InReview` | Claim is actively being evaluated by an adjuster. |
| `Approved` | Claim has been approved for payment processing. |
| `**Suspended**` | *(new)* Claim has been suspended due to fraud suspicion. All downstream payment activity is frozen. |
| `Rejected` | Claim has been denied. |
| `Closed` | Claim lifecycle is complete. |

**Rationale for placement in the domain:** `ClaimStatus` is a pure value object embedded in the domain model. Its extension requires no infrastructure changes; it is the authoritative source of truth for all state-transition guards throughout the system.

---

### 1.2 `Claim` Entity — Changes and New Invariants

#### 1.2.1 New State Fields

Three new fields must be added to the `Claim` aggregate root:

- **`FraudSuspicionReason`** — A non-empty, free-text or enumerated value capturing the stated reason for suspicion. Mandatory when suspending. Must not exceed a defined character limit (suggested: 1 000 characters) to prevent misuse as a general notes field.
- **`SuspendedAt`** — A UTC timestamp recording the exact moment suspension was applied. Set once; immutable thereafter.
- **`SuspendedByUserId`** — The identity of the claims manager who triggered the suspension. Provides a full audit trail without relying solely on infrastructure-level logging.

#### 1.2.2 Valid State Transitions

The transition graph must be updated. The only transitions **into** `Suspended` that are permitted are:

| From | To | Guard Condition |
|---|---|---|
| `Submitted` | `Suspended` | Fraud reason provided |
| `InReview` | `Suspended` | Fraud reason provided |
| `Approved` | `Suspended` | Fraud reason provided; see invariant note below |

Transitions that must be **explicitly blocked** at the domain level (guard must throw a domain exception):

- `Draft → Suspended` — A claim that has never been submitted cannot logically be suspected of fraud in the payment pipeline.
- `Rejected → Suspended` — A rejected claim is already terminal.
- `Closed → Suspended` — A closed claim is already terminal.
- `Suspended → Suspended` — Re-suspending an already-suspended claim is a no-op that must be rejected with a descriptive domain exception to prevent accidental duplicate commands.

#### 1.2.3 New Domain Method: `Suspend`

A dedicated method on the `Claim` aggregate encapsulates the suspension business logic. Its responsibilities, in order, are:

1. **Guard — already suspended:** If `Status == Suspended`, raise a domain exception with the message that the claim is already suspended.
2. **Guard — invalid source state:** If `Status` is not one of the permitted source states (`Submitted`, `InReview`, `Approved`), raise a domain exception describing the illegal transition.
3. **Guard — reason required:** If `FraudSuspicionReason` is null, empty, or whitespace, raise a domain exception. The reason is never optional.
4. **State mutation:** Set `Status = Suspended`, `SuspendedAt = UtcNow`, `SuspendedByUserId = actorId`, and `FraudSuspicionReason = reason`.
5. **Payment blocking:** Iterate over all associated `Payment` entities that carry a `Pending` status and transition each to `Blocked`. This mutation happens inside the aggregate so that the invariant is enforced atomically — no payment can remain `Pending` after its parent claim is suspended.
6. **Raise domain event:** Append a `ClaimSuspendedDomainEvent` to the aggregate's internal event collection. This event carries the claim identifier, the suspension timestamp, the actor, and the reason.

#### 1.2.4 Invariants While in `Suspended` State

Once a claim is suspended, the following mutations must be rejected at the domain level until an explicit "Unsuspend" or "Reactivate" feature is implemented:

- **No new payments may be created** for a suspended claim. Any attempt raises a domain exception.
- **No existing payments may transition out of `Blocked`** through normal processing paths. Only a future "lift suspension" command may do so.
- **The `FraudSuspicionReason` field is immutable** after suspension is applied. Updates to it require a separate, audited command that is out of scope for this story.
- **The `ApprovedAmount` and coverage fields are read-only** in the suspended state, preventing backdoor manipulation while the claim is frozen.

#### 1.2.5 `ClaimSuspendedDomainEvent` — Contents

The domain event raised by the `Suspend` method must carry:

- `ClaimId` — the aggregate identifier.
- `SuspendedAt` — UTC timestamp.
- `SuspendedByUserId` — actor identifier.
- `FraudSuspicionReason` — the stated reason.
- `BlockedPaymentIds` — a list of all payment identifiers that were transitioned to `Blocked` as a side effect.

Publishing the blocked payment IDs in the event allows downstream subscribers (notifications, audit log, payment processor gateway) to react without re-querying the database.

---

## 2. Application Layer

### 2.1 `SuspendClaimCommand` — Structure and Responsibilities

`SuspendClaimCommand` is a MediatR `IRequest<SuspendClaimResult>`. It is a plain data carrier with no logic.

**Required input properties:**

| Property | Type | Notes |
|---|---|---|
| `ClaimId` | Identifier (GUID) | The claim to be suspended. |
| `FraudSuspicionReason` | String | Mandatory; will be passed through to the domain. |
| `ActorUserId` | Identifier | The authenticated user issuing the command. Used for audit. |

**`SuspendClaimResult`** — the success response shape:

| Property | Notes |
|---|---|
| `ClaimId` | Echo of the suspended claim. |
| `SuspendedAt` | The timestamp recorded by the domain. |
| `BlockedPaymentCount` | How many payments were blocked as a side effect, for confirmation messaging in the UI. |

---

### 2.2 Validation Rules and Business Pre-conditions

Validation is performed in two layers, executed sequentially before the domain method is invoked.

#### Layer 1 — Input Validation (FluentValidation or equivalent, Application Layer)

These rules are enforced before the aggregate is even loaded:

1. `ClaimId` must not be empty.
2. `FraudSuspicionReason` must not be null or whitespace.
3. `FraudSuspicionReason` must not exceed 1 000 characters.
4. `ActorUserId` must not be empty.

Failure at this layer returns a structured validation error response without touching the database.

#### Layer 2 — Authorization Pre-condition (Application Layer, before domain call)

5. The actor identified by `ActorUserId` must hold the **Claims Manager** role. If not, the command must be rejected with a `403 Forbidden`-equivalent result before the aggregate is loaded.

#### Layer 3 — Domain Invariants (enforced by the aggregate itself)

These are not re-validated in the application layer; they are delegated entirely to the `Claim.Suspend()` domain method:

6. The claim must exist in the repository (a missing claim yields a `NotFound` result, not a domain exception).
7. The claim must be in a suspendable state (enforced by domain guard — see Section 1.2.2).
8. The claim must not already be suspended (enforced by domain guard).

**Separation rationale:** Input validation and authorization belong to the application layer because they do not require domain knowledge. State-transition correctness is a domain concern and must live in the aggregate.

---

### 2.3 Handler Execution Workflow

The `SuspendClaimCommandHandler` follows this step-by-step flow:

**Step 1 — Validate input.**
Run FluentValidation against the command. If validation fails, return a failure result immediately. Do not proceed.

**Step 2 — Authorize.**
Query the authorization service with `ActorUserId` and the required `SuspendClaim` permission. If authorization fails, return a `Forbidden` result. Do not proceed.

**Step 3 — Load aggregate.**
Retrieve the `Claim` aggregate from the repository by `ClaimId`, including its associated `Payment` collection. If the claim does not exist, return a `NotFound` result.

**Step 4 — Invoke domain method.**
Call `claim.Suspend(reason: command.FraudSuspicionReason, actorId: command.ActorUserId)` on the loaded aggregate. This single call atomically:
- Validates the state transition.
- Marks all pending payments as blocked.
- Appends the `ClaimSuspendedDomainEvent` to the aggregate's uncommitted events.

If the domain method raises a domain exception (e.g., already suspended, invalid transition), catch it and return a `DomainError` result. Do not persist.

**Step 5 — Persist.**
Pass the mutated aggregate to the repository's `Save` (or `Update`) method. The persistence layer is responsible for writing all changes atomically — the updated claim status, the new suspension fields, and every payment status change — within a single database transaction. No partial saves are acceptable.

**Step 6 — Dispatch domain events.**
After the transaction commits successfully, dispatch the `ClaimSuspendedDomainEvent` through the domain event dispatcher. This is deliberately post-commit to avoid sending notifications for transactions that ultimately fail.

**Step 7 — Return result.**
Construct and return a successful `SuspendClaimResult`, including the `ClaimId`, the `SuspendedAt` timestamp from the aggregate, and the count of payments that were blocked.

---

### 2.4 Side-Effect Coordination via Domain Event

The `ClaimSuspendedDomainEvent` emitted in Step 6 is consumed by one or more event handlers registered outside the command pipeline. These handlers must be idempotent:

- **`NotifyClaimsManagerOnSuspensionHandler`** — sends an internal notification (email, in-app) to relevant stakeholders.
- **`AuditLogSuspensionHandler`** — writes a structured audit entry to the audit store, including all blocked payment IDs.
- **`PaymentGatewayRecallHandler`** *(if applicable)* — if any blocked payment was already dispatched to an external payment processor but not yet settled, this handler issues a cancellation request to the gateway.

---

## 3. Infrastructure & Tests

### 3.1 Data Integrity Strategy — Impact on Payment Synchronization Pipeline

#### 3.1.1 The Core Problem

The payment synchronization pipeline (`ClaimPaymentSyncTests.cs`) is designed to periodically reconcile the state of pending payments between the internal system and external payment processors or ledger systems. Without modification, this pipeline would be unaware of the `Blocked` payment status introduced by this feature and could inadvertently re-queue or re-process blocked payments, violating the suspension invariant at the infrastructure level.

#### 3.1.2 Required Changes to the Sync Pipeline

The synchronization pipeline must be modified at its filtering stage — the point where it queries for payments eligible for processing — to explicitly exclude payments in the `Blocked` status. This is a **mandatory guard** and must be implemented as follows:

- **Query-level exclusion:** The repository query that fetches payments for synchronization must include a status filter that omits `Blocked` payments. This is the primary defense.
- **Processor-level guard (defense in depth):** Even if a `Blocked` payment somehow reaches the sync processor (e.g., due to a race condition between a suspension command and an in-flight sync job), the processor must check the payment's current status before executing any mutation. If the status is `Blocked`, the processor must skip the payment and log a warning — it must not throw, as this would interrupt processing of other payments in the same batch.
- **Parent claim status check (defense in depth):** The sync pipeline should additionally verify that the parent claim is not in `Suspended` status before processing any of its payments. This catches edge cases where a `Blocked` status may not have propagated correctly.

#### 3.1.3 Race Condition Mitigation

A race condition exists between a suspension command and an in-flight sync job: the sync job may have already loaded a batch of `Pending` payments before the suspension committed. The processor-level guard described above is the mitigation for this scenario. No distributed lock is required because the guard is purely read-based and results in a skip, not a rollback.

#### 3.1.4 Expected Behavior in `ClaimPaymentSyncTests.cs`

The existing test suite must be extended to assert the following behaviors:

- When the sync pipeline runs against a claim in `Suspended` status, **zero payments from that claim are processed or mutated**.
- When the sync pipeline runs against a batch that contains a mix of active-claim payments and suspended-claim payments, only the active-claim payments are processed; suspended-claim payments are skipped without error.
- A sync run that skips blocked payments must produce an observable audit/log entry per skipped payment for traceability.
- The sync pipeline's return value or metrics output must distinguish between "processed," "skipped-blocked," and "failed" counts.

---

### 3.2 Test Scenarios

All tests follow the Given / When / Then structure. Tests are organized by layer.

---

#### 3.2.1 Domain Unit Tests — `Claim` Aggregate

**Scenario 1 — Happy path: suspend an approved claim**
- **Given** a claim in `Approved` status with two `Pending` payments
- **When** `Suspend` is called with a valid reason and a valid actor ID
- **Then** the claim status becomes `Suspended`
- **And** `SuspendedAt` is set to a UTC timestamp
- **And** `SuspendedByUserId` matches the provided actor ID
- **And** `FraudSuspicionReason` matches the provided reason
- **And** both payments transition to `Blocked`
- **And** a `ClaimSuspendedDomainEvent` is raised containing both blocked payment IDs

**Scenario 2 — Happy path: suspend an in-review claim with no payments**
- **Given** a claim in `InReview` status with zero payments
- **When** `Suspend` is called with a valid reason
- **Then** the claim status becomes `Suspended`
- **And** `BlockedPaymentIds` in the domain event is an empty collection
- **And** no domain exception is raised

**Scenario 3 — Happy path: suspend a submitted claim with one pending payment**
- **Given** a claim in `Submitted` status with one `Pending` payment
- **When** `Suspend` is called with a valid reason
- **Then** the claim status becomes `Suspended` and the payment becomes `Blocked`

**Scenario 4 — Guard: cannot suspend a draft claim**
- **Given** a claim in `Draft` status
- **When** `Suspend` is called
- **Then** a domain exception is raised indicating the transition from `Draft` to `Suspended` is not permitted
- **And** the claim status remains `Draft`

**Scenario 5 — Guard: cannot suspend a rejected claim**
- **Given** a claim in `Rejected` status
- **When** `Suspend` is called
- **Then** a domain exception is raised indicating the claim is in a terminal state
- **And** the claim status remains `Rejected`

**Scenario 6 — Guard: cannot suspend a closed claim**
- **Given** a claim in `Closed` status
- **When** `Suspend` is called
- **Then** a domain exception is raised indicating the claim is in a terminal state

**Scenario 7 — Guard: cannot re-suspend an already-suspended claim**
- **Given** a claim already in `Suspended` status
- **When** `Suspend` is called again
- **Then** a domain exception is raised stating the claim is already suspended
- **And** no new domain event is raised
- **And** the existing `SuspendedAt` timestamp is unchanged

**Scenario 8 — Guard: cannot create a new payment for a suspended claim**
- **Given** a claim in `Suspended` status
- **When** an attempt is made to add a new payment to the claim
- **Then** a domain exception is raised blocking the operation

**Scenario 9 — Guard: only pending payments are blocked, not already-processed ones**
- **Given** a claim in `Approved` status with one `Pending` payment and one `Completed` payment
- **When** `Suspend` is called
- **Then** only the `Pending` payment transitions to `Blocked`
- **And** the `Completed` payment status remains unchanged
- **And** the domain event's `BlockedPaymentIds` contains only the formerly `Pending` payment

**Scenario 10 — Guard: empty fraud reason is rejected**
- **Given** a claim in `InReview` status
- **When** `Suspend` is called with a null or empty reason
- **Then** a domain exception is raised requiring a reason
- **And** the claim status is unchanged

---

#### 3.2.2 Application Layer Unit Tests — `SuspendClaimCommandHandler`

**Scenario 11 — Validation failure: missing claim ID**
- **Given** a command with an empty `ClaimId`
- **When** the handler processes the command
- **Then** a validation failure result is returned
- **And** the repository is never called

**Scenario 12 — Validation failure: reason exceeds character limit**
- **Given** a command with a `FraudSuspicionReason` of 1 001 characters
- **When** the handler processes the command
- **Then** a validation failure result is returned

**Scenario 13 — Authorization failure: actor lacks Claims Manager role**
- **Given** a valid command where the `ActorUserId` belongs to a user without the Claims Manager role
- **When** the handler processes the command
- **Then** a `Forbidden` result is returned
- **And** the repository is never called

**Scenario 14 — Not found: claim does not exist**
- **Given** a valid command with a `ClaimId` that does not exist in the repository
- **When** the handler processes the command
- **Then** a `NotFound` result is returned

**Scenario 15 — Domain error propagation: invalid state transition**
- **Given** a valid command and an authorized actor
- **And** the repository returns a claim in `Draft` status
- **When** the handler processes the command
- **Then** the domain exception from `Claim.Suspend()` is caught
- **And** a `DomainError` result is returned
- **And** the repository's Save method is never called

**Scenario 16 — Happy path: command completes successfully**
- **Given** a valid command, an authorized actor, and a repository returning an `Approved` claim with two pending payments
- **When** the handler processes the command
- **Then** the repository's Save method is called once with the mutated aggregate
- **And** the domain event is dispatched after the save
- **And** the returned result contains `BlockedPaymentCount = 2`

**Scenario 17 — Atomicity: repository failure does not dispatch event**
- **Given** a valid command and a repository that throws on Save
- **When** the handler processes the command
- **Then** the domain event dispatcher is never called
- **And** a failure result is returned to the caller

---

#### 3.2.3 Infrastructure / Integration Tests — Payment Sync Pipeline

**Scenario 18 — Sync skips all blocked payments**
- **Given** the sync pipeline is run
- **And** the database contains two `Blocked` payments belonging to a `Suspended` claim
- **When** the sync job executes
- **Then** neither payment is mutated
- **And** the sync output reports both as "skipped-blocked"

**Scenario 19 — Sync processes active payments unaffected by a sibling suspension**
- **Given** the database contains three claims: one `Suspended` with blocked payments, one `Approved` with pending payments, one `InReview` with no payments
- **When** the sync job executes
- **Then** only the `Approved` claim's pending payments are processed
- **And** the `Suspended` claim's payments are skipped

**Scenario 20 — Race condition guard: sync encounters a blocked payment mid-batch**
- **Given** a sync batch was loaded containing a payment that was `Pending` at query time
- **And** before the processor reaches that payment, a suspension command has committed and set the payment to `Blocked`
- **When** the processor's per-payment guard re-checks the current status
- **Then** the payment is skipped without error
- **And** the remaining batch continues processing normally

**Scenario 21 — Audit log: skipped blocked payments are traceable**
- **Given** the sync pipeline skips a blocked payment
- **When** the run completes
- **Then** the audit/log output contains an entry for the skipped payment with its ID, its `Blocked` status, and the run timestamp

**Scenario 22 — Sync metrics: blocked skips are counted separately**
- **Given** a sync run that processes 5 payments and skips 3 blocked payments
- **When** the run completes
- **Then** the metrics output reports `processed = 5`, `skipped_blocked = 3`, `failed = 0`

---

#### 3.2.4 Functional / End-to-End Test Scenarios

**Scenario 23 — Full suspension flow via API**
- **Given** an authenticated Claims Manager
- **And** an existing claim in `InReview` status with two pending payments, accessible via the API
- **When** the manager submits a valid suspend request
- **Then** the API returns a `200 OK` with the suspension timestamp and `blockedPaymentCount = 2`
- **And** a subsequent GET on the claim returns `status = Suspended`
- **And** a subsequent GET on each payment returns `status = Blocked`

**Scenario 24 — Unauthorized user cannot suspend via API**
- **Given** an authenticated user without the Claims Manager role
- **When** the user submits a suspend request
- **Then** the API returns `403 Forbidden`
- **And** the claim status is unchanged in the database

**Scenario 25 — Idempotency check: double suspend via API**
- **Given** a claim that has already been suspended
- **When** a Claims Manager submits a second suspend request for the same claim
- **Then** the API returns a `422 Unprocessable Entity` (or equivalent domain error response)
- **And** the existing suspension data is unchanged

---

## Appendix — Architectural Decisions (Resolved)

The following decisions have been confirmed by the team and product owner.

| # | Decision | Resolution |
|---|---|---|
| 1 | Unsuspend / Lift Suspension | **In scope.** A `LiftSuspensionCommand` must be planned and delivered in the same sprint. `Suspended` is not terminal. |
| 2 | `Approved → Suspended` transition | **Requires escalated authorization.** This transition must be gated behind an additional confirmation flag on the command and a dedicated elevated permission (distinct from the standard `SuspendClaim` permission). |
| 3 | Payment gateway cancellation | **In scope.** The `PaymentGatewayRecallHandler` must be implemented as part of this story, not deferred. |
| 4 | Character limit on `FraudSuspicionReason` | **Confirmed at 1 000 characters.** This value is now a hard domain constraint, not a suggestion. |
| 5 | Notification recipients | **All stakeholders.** The suspension notification must be sent to the submitting manager, all assigned adjusters, supervisors, and the claimant. |

### Impact of Resolutions on the Plan

**Decision 1 — `LiftSuspensionCommand`:**
A `LiftSuspension` command must be added to the same sprint backlog. At the domain level, the `Claim` aggregate must expose a `LiftSuspension()` method that mirrors the `Suspend()` guards in reverse: it must only be callable from the `Suspended` state, require a reinstatement reason, and transition all `Blocked` payments back to `Pending`. A `ClaimSuspensionLiftedDomainEvent` must be raised to allow the payment sync pipeline to resume processing those payments.

**Decision 2 — Escalated authorization for `Approved → Suspended`:**
The `SuspendClaimCommand` must include an optional `EscalatedConfirmation` boolean flag. The handler's authorization step must branch: if the loaded claim is in `Approved` status, it must verify that the actor holds the elevated `SuspendApprovedClaim` permission **and** that `EscalatedConfirmation` is explicitly set to `true`. If either condition is missing, the command is rejected before the domain method is invoked. This logic lives in the application layer, not the domain, as it is an authorization concern rather than a business invariant.

**Decision 3 — `PaymentGatewayRecallHandler` is in scope:**
This handler must be implemented and registered as a subscriber to `ClaimSuspendedDomainEvent` within this sprint. It must use the `BlockedPaymentIds` from the event to issue cancellation requests to the external payment gateway for any payment that had already been dispatched but not yet settled. The handler must be idempotent: a recall request for a payment already cancelled at the gateway must log a warning and succeed rather than fault. Retry and dead-letter policies for gateway communication failures must be defined with the infrastructure team.

**Decision 4 — 1 000-character limit is now a hard constraint:**
The limit is no longer advisory. It must be enforced at three levels: the FluentValidation rule on the command (application layer), a domain guard inside `Claim.Suspend()` (domain layer), and a database column constraint on the persistence model (infrastructure layer). All three must be consistent.

**Decision 5 — Notification recipients are all stakeholders:**
The `NotifyClaimsManagerOnSuspensionHandler` must be renamed to `NotifyStakeholdersOnSuspensionHandler` to reflect its expanded scope. It must resolve and notify: the submitting manager (from `SuspendedByUserId`), all adjusters currently assigned to the claim, all supervisors in the claim's organizational unit, and the claimant (via their registered contact channel — email or in-app). Notification content must include the claim reference, the suspension timestamp, and a redacted version of the fraud suspicion reason appropriate for each recipient type (full reason for internal staff; a generic "under review" message for the claimant).