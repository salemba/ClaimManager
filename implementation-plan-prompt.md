# Advanced Framing Prompt: Suspend Claim Feature (Fraud Detection)

## 📋 User Story Context
**Epic:** Claims Management & Fraud Prevention  
**Feature:** Claim Suspension with Automatic Payment Blocking  

```
As a claims adjuster,
I want to be able to suspend a claim (Status: Suspended) when fraud is suspected,
So that any associated in-flight payments are automatically blocked and the claim workflow is halted.
```

---

## 🔴 CRITICAL CONSTRAINT: NO C# CODE GENERATION
**This phase is PLANNING ONLY.** Do NOT generate, modify, or suggest any C# code, including:
- Entity method implementations
- Command/Query classes
- Validator code
- Test cases (even pseudocode)
- API endpoint signatures
- Database migration code

Output ONLY conceptual design, architecture diagrams (text-based), decision matrices, and implementation roadmaps.

---

## 📁 Reference Context Files

### Domain Layer - Current State
- **File:** `src/ClaimManager.Domain/Claims/Entities/Claim.cs`
- **Key:** The `Claim` entity uses string-based status (not enum). Status transitions are managed by a static dictionary `_advanceTransitions` that defines valid state flows.
- **Current Statuses:** `"new"`, `"open"`, `"in-review"`, `"approved"`, `"closed"`
- **Invariants:** Status changes are guarded by methods like `AdvanceWorkflow()` and `GetAvailableActions()`. External dependencies are tracked via `ActiveDataIntegrityIssuesJson` and `HasDataIntegrityWarning`.

### Infrastructure Test Strategy - Current Patterns
- **File:** `tests/ClaimManager.Domain.UnitTests/Claims/ClaimPaymentSyncTests.cs`
- **Key:** Tests verify that payment sync preserves claim invariants. Tests check for data integrity warnings, warning clearing, and audit trail generation.
- **Pattern:** Test methods use factory pattern (`CreateClaim()`) and verify state transitions, return values (audit summaries), and side effects.

### API Application Layer - Localization & Audit
- **File:** `src/ClaimManager.Api/Endpoints/Claims/ClaimsController.cs`
- **Key:** All user-facing messages are externalized via `IStringLocalizer` and keys stored in `ClaimsController.resx`. Audit trails are created via `RecordClaimAuditCommand`. Payment-related operations are wrapped in try-catch blocks with failure reason tracking.

---

## 🎯 Design Tasks (PLANNING PHASE ONLY)

### Task 1: Domain Layer Design
**Objective:** Document how the suspension feature integrates with the Claim entity's state machine and invariants.

**Questions to Answer:**
1. Where does `"suspended"` status fit in the current `_advanceTransitions` state machine? Can it be reached from any status or only specific ones?
2. What happens to blocking-related fields (`BlockerType`, `BlockerReason`) when a claim is suspended? Should they be repurposed or extended?
3. How should the suspension be tracked? Should there be timestamps (e.g., `SuspendedAtUtc`, `SuspendedByUserId`)? Where in the Claim entity properties?
4. What invariants must hold when a claim is suspended? (e.g., no new notes allowed? no document uploads?)
5. Should suspension be reversible? If yes, what's the unsuspend workflow?

**Output Format:**
- State diagram showing `suspended` position in the workflow
- List of new properties required (if any) or reuse strategy
- Invariant rules table: (Rule | Impact | Affected Methods)

---

### Task 2: Application Layer Design
**Objective:** Define the command structure, validation rules, and business logic orchestration for the SuspendClaim feature.

**Questions to Answer:**
1. Should suspension be triggered by a dedicated `SuspendClaimCommand` or as part of an existing "block claim" abstraction?
2. What validation rules must pass before suspension is allowed?
   - Must the claim be in a specific status? (e.g., `"open"`, `"in-review"` only)
   - Is there a minimum/maximum fraud confidence threshold?
   - Can only certain roles (e.g., senior adjusters) suspend?
3. When a claim is suspended, what downstream effects must the application orchestrate?
   - Should we issue a command to the Payment System to cancel in-flight payments?
   - Should we notify the claimant? (If yes, which message template?)
   - Should we create a claim note automatically documenting the suspension reason?
4. What is the audit trail structure? (actor, action, timestamp, reason)
5. Error scenarios: What if the payment cancellation fails? Rollback suspension or proceed with a data integrity warning?

**Output Format:**
- Command/Query structure diagram (inputs → validations → side effects → outputs)
- Validation rules matrix: (Rule | Type | Source | Failure Reason Key in .resx)
- Integration touchpoints table: (Target System | Action | Sync/Async | Error Handling)

---

### Task 3: Infrastructure & Test Strategy
**Objective:** Define test scenarios, mocking strategy, and data integrity handling for payment blocking.

**Questions to Answer:**
1. **Unit Test Scenarios (Domain Layer):**
   - Can a claim transition to `"suspended"` from each valid source status?
   - Are `BlockerType` and `BlockerReason` correctly set/cleared during suspension transitions?
   - Do GetAvailableActions() correctly exclude incompatible actions (e.g., "advance", "route-for-approval") when suspended?

2. **Integration Test Scenarios (Application Layer):**
   - When `SuspendClaimCommand` executes, does it correctly call the Payment System API to halt payments?
   - If the external payment cancellation fails, is the claim marked with a data integrity warning? Does suspension still proceed?
   - Are audit entries created with the correct localization keys?

3. **Functional Test Scenarios (API Layer - ClaimManager.Api.FunctionalTests):**
   - Can an authorized adjuster POST to `/api/claims/{id}/suspend` with a fraud reason?
   - Does the response correctly reflect the updated status and blocker fields?
   - Are subsequent payment sync attempts correctly rejected/blocked for a suspended claim?

4. **Mocking Strategy (Existing Pattern):**
   - Current tests use `ClaimManagerApiFactory` to set up WebApplicationFactory. Should we extend it to mock payment system responses?
   - For payment blocking: should we mock `IPaymentSystemClient.CancelPaymentAsync()` to return success/failure scenarios?

5. **Data Integrity Handling:**
   - If payment system is unreachable when suspending, should we record a data integrity warning with key like `"Suspend_PaymentCancellationFailed"`?
   - Should there be a reconciliation/retry mechanism (like existing `ReconcileClaimStateAsync`) to retry payment cancellation later?

**Output Format:**
- Test matrix: (Layer | Scenario | Preconditions | Expected Outcome | Mock Setup Required?)
- Dependency injection diagram: (Command Handler → Payment System Client → External API)
- Error path decision tree: (Payment cancellation fails? → Rollback | Mark warning? → Log | Notify?)

---

## 📊 Architecture Alignment Checklist

Before proceeding to implementation, validate against Clean Architecture layers:

**✓ Domain Layer (ClaimManager.Domain)**
- [ ] New status `"suspended"` added to valid state transitions
- [ ] New domain event or value object for suspension tracking (if needed)
- [ ] Invariants documented in comments
- [ ] No external dependency knowledge in entity methods

**✓ Application Layer (ClaimManager.Application)**
- [ ] `SuspendClaimCommand` & `SuspendClaimCommandValidator` defined
- [ ] Command handler orchestrates Payment System client call
- [ ] Audit trail command created (`RecordClaimAuditCommand`)
- [ ] Error handling strategy defined (rollback vs. warn)

**✓ Infrastructure Layer (ClaimManager.Infrastructure)**
- [ ] Payment System client method for canceling payments (if not present)
- [ ] Transaction handling for rollback scenarios
- [ ] Retry policy for failed payment cancellations

**✓ API Layer (ClaimManager.Api)**
- [ ] Endpoint: `POST /api/claims/{id}/suspend` with authorization check
- [ ] Localization keys registered in `ClaimsController.resx`
- [ ] Functional test in `ClaimManager.Api.FunctionalTests`

**✓ Test Coverage (ClaimManager.*.UnitTests / IntegrationTests)**
- [ ] Domain: State transitions tested
- [ ] Application: Validation & orchestration tested
- [ ] API: Authorization & response format tested

---

## 🚀 Next Steps (Post-Planning)

Once this planning phase is approved:
1. Implement Domain layer changes (Claim entity status + invariants)
2. Implement Application layer (SuspendClaimCommand + validator)
3. Implement Infrastructure changes (Payment cancellation logic)
4. Implement API endpoint + tests
5. Code review against architecture boundaries
6. Integration testing with payment system mock

---

## 📝 Design Review Checklist

Before generating code, confirm:
- [ ] State machine diagram is reviewed and approved
- [ ] Validation rules are unambiguous
- [ ] Error handling strategy for payment system failures is agreed upon
- [ ] Localization keys identified and consistent with existing patterns
- [ ] Test scenarios cover happy path, edge cases, and failure scenarios
- [ ] No cross-layer concerns (e.g., API calling domain directly)
- [ ] Audit trail includes all required fields for compliance

