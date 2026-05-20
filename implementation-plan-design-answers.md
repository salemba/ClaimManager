# Design Answers: Suspend Claim Feature (Fraud Detection)

## 🏗️ Task 1: Domain Layer Design

### 1. State Machine Integration
The `"suspended"` status is an exception to the normal flow. It should NOT be part of the sequential `_advanceTransitions` dictionary. Instead, it is a cross-cutting state:
- **Valid source states:** `"new"`, `"open"`, `"in-review"`, `"approved"`.
- **Invalid source state:** `"closed"`.

**State Diagram:**
```text
[new] ------\
[open] ------\
[in-review] ----> [suspended]
[approved] --/
```

### 2. Blocker Fields Strategy
We will **reuse** the existing `BlockerType` and `BlockerReason` fields to avoid redundant database columns.
- `BlockerType` becomes `"fraud-suspension"`.
- `BlockerReason` captures the fraud suspicion rationale provided by the adjuster.

### 3. Suspension Tracking Properties
To guarantee data integrity and reversibility, three new properties must be added to the `Claim` entity:
- `DateTime? SuspendedAtUtc` (Timestamp of suspension)
- `string? SuspendedByUserId` (Trace of the actor)
- `string? PreviousStatus` (To remember the exact state before suspension, allowing a clean unsuspend)

### 4. Invariant Rules
| Rule | Impact | Affected Methods |
|------|--------|------------------|
| **Workflow Blocked** | Cannot advance workflow | `AdvanceWorkflow()`, `RouteForApproval()` |
| **Payments Blocked** | Cannot apply new payments | `ApplyPaymentData()` |
| **Evidence Allowed** | Adding notes/documents remains active | `AddNote()`, `ApplyDocumentSync()` |
*Note: `GetAvailableActions()` must return an empty list for workflow actions when suspended.*

### 5. Reversibility
Suspension must be reversible. An `Unsuspend()` method is required to:
- Restore `Status` to the value in `PreviousStatus`.
- Clear `BlockerType` and `BlockerReason`.
- Clear `SuspendedAtUtc`, `SuspendedByUserId`, and `PreviousStatus`.

---

## ⚙️ Task 2: Application Layer Design

### 1. Command Structure
A dedicated `SuspendClaimCommand` is mandatory. Fraud suspension triggers critical side effects (external payment cancellation) that standard administrative blocking does not possess.

**Command Structure Diagram:**
```text
[SuspendClaimCommand(ClaimId, Reason)] 
   → [Validator] 
   → [Cancel External Payments] 
   → [Update Claim Entity] 
   → [Create ClaimNote] 
   → [Create ClaimAudit]
```

### 2. Validation Rules Matrix
| Rule | Type | Source | Failure Reason Key (.resx) |
|------|------|--------|----------------------------|
| Cannot be closed | State | Entity | `Err_CannotSuspendClosedClaim` |
| Cannot be already suspended | State | Entity | `Err_ClaimAlreadySuspended` |
| Reason required | Input | Validator | `Err_SuspensionReasonRequired` |
| Reason min length (10 chars) | Input | Validator | `Err_SuspensionReasonTooShort` |
| Requires Adjuster Policy | Auth | API | `Err_AuthenticationRequired` |

### 3. Downstream Effects Orchestration
| Target System | Action | Sync/Async | Error Handling |
|---------------|--------|------------|----------------|
| Payment System | `CancelInFlightPaymentsAsync` | Sync | Log warning, do NOT rollback |
| ClaimNotes | Create auto-note | Sync | Rollback transaction on fail |
| Claimant Notification | NONE (Do not alert suspect) | N/A | N/A |

### 4. Audit Trail Structure
- **Action:** `"claim-suspended"`
- **Summary:** Localized key `"Audit_ClaimSuspended"` (includes reason)
- **Actor:** Adjuster ID triggering the command

### 5. Error Scenarios: Payment Cancellation Failure
Local suspension (database) takes absolute priority. If the payment API fails or times out:
- **Do NOT rollback** the claim suspension.
- Register a data integrity warning via `UpsertDataIntegrityIssue("payment", "Failed to cancel external payment during suspension")`.

---

## 🧪 Task 3: Infrastructure & Test Strategy

### 1. Unit Test Scenarios (Domain Layer)
- Transition to `"suspended"` succeeds from `"open"`, fails from `"closed"`.
- `GetAvailableActions()` excludes workflow transitions when suspended.
- `Unsuspend()` accurately restores previous state and clears blocker fields.

### 2. Integration Test Scenarios (Application Layer)
- **Happy Path:** Command modifies DB, successfully calls `PaymentSystemClient` mock, creates `ClaimAudit`.
- **Degraded Path:** Command executes, `PaymentSystemClient` throws exception, DB saves suspension but adds active `DataIntegrityWarning`.

### 3. Functional Test Scenarios (API Layer)
- `POST /api/claims/{id}/suspend` with valid payload returns `200 OK`.
- `POST /api/claims/{id}/advance` on a suspended claim returns `400 Bad Request` (or `409 Conflict`).

### 4. Mocking Strategy
- Add `CancelInFlightPaymentsAsync(string claimNumber)` to `IPaymentSystemClient`.
- In `ClaimManagerApiFactory`, configure `Mock<IPaymentSystemClient>` to simulate success (`Task.CompletedTask`) and failure (`HttpRequestException`).

### 5. Data Integrity Handling
- The existing nightly reconciliation (`ReconcileClaimStateAsync`) must be updated.
- If it detects a `"suspended"` claim with a `"payment"` issue, it must retry the external payment cancellation until successful, then clear the warning.