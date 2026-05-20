# Implementation Plan: Suspend Claim & Block Payments

## Context
**User Story:** As a claims manager, I want to be able to suspend a claim (Status: Suspended) if fraud is suspected. This suspension must automatically block the execution of any associated payments in progress.

**Architecture:** Clean Architecture with Domain-Driven Design principles
**Pattern:** CQRS with MediatR
**Constraint:** No C# code production in this iteration - planning phase only

---

## 1. Domain Layer

### 1.1 ClaimStatus.cs - Enumeration Modification

**File:** `src/Domain/Enums/ClaimStatus.cs`

**Changes Required:**
- Add `Suspended` value to the `ClaimStatus` enumeration
- Ensure proper ordering and documentation

**Invariant Rules:**
- `Suspended` state indicates a claim under fraud investigation
- Claims in `Suspended` state cannot progress through normal workflow
- Suspension requires explicit reactivation to resume processing

### 1.2 Claim.cs - Entity Modification

**File:** `src/Domain/Entities/Claim.cs`

**Changes Required:**
- Add `Suspend(string reason, string suspendedByUserId, DateTime suspendedAt)` method
- Add `Reactive()` method for reactivating suspended claims
- Add properties:
  - `SuspensionReason` (string, nullable)
  - `SuspendedByUserId` (string, nullable)
  - `SuspendedAt` (DateTime?, nullable)

**Domain Invariants:**
1. **State Transition Rule:** Can only suspend claims in `Active`, `UnderReview`, or `PendingApproval` states
2. **Suspension Reason:** Must provide a reason for suspension (min 10 characters)
3. **No Double Suspension:** Cannot suspend an already suspended claim
4. **Audit Trail:** Must capture who suspended the claim and when

**Domain Events:**
- Raise `ClaimSuspendedDomainEvent` with:
  - `ClaimId`
  - `SuspendedBy`
  - `SuspensionReason`
  - `Timestamp`

---

## 2. Application Layer

### 2.1 SuspendClaimCommand.cs

**File:** `src/Application/Claims/Commands/SuspendClaimCommand.cs`

**Command Structure:**

Properties:
ClaimId: Guid (required)
SuspensionReason: string (required, min 10 chars, max 500 chars)
SuspendedByUserId: string (required)


**Validation Rules:**
- `ClaimId` must be a valid GUID
- `SuspensionReason` is required and must be between 10-500 characters
- `SuspendedByUserId` must not be empty
- User must have "ClaimsManager" or "FraudInvestigator" role

### 2.2 SuspendClaimCommandHandler.cs

**File:** `src/Application/Claims/Commands/SuspendClaimCommandHandler.cs`

**Handler Logic:**
1. Retrieve claim aggregate from repository by `ClaimId`
2. Validate claim exists and is in a suspendable state
3. Call `claim.Suspend()` domain method
4. Persist changes to repository
5. Publish domain events (MediatR will handle this automatically)
6. Return success result with updated claim status

**Error Handling:**
- `ClaimNotFoundException` if claim doesn't exist
- `InvalidClaimStateException` if claim cannot be suspended
- `UnauthorizedAccessException` if user lacks permissions

### 2.3 SuspendClaimCommandValidator.cs

**File:** `src/Application/Claims/Commands/SuspendClaimCommandValidator.cs`

**FluentValidation Rules:**
- `ClaimId`: NotEmpty, must be valid GUID format
- `SuspensionReason`: NotEmpty, MinimumLength(10), MaximumLength(500)
- `SuspendedByUserId`: NotEmpty, NotNull

---

## 3. Infrastructure & Tests

### 3.1 Payment Blocking Integration Strategy

**Impact Analysis:**
When a claim is suspended, all associated payments must be blocked immediately to prevent financial loss during fraud investigation.

**Integration Points:**

1. **Domain Event Handler:** `ClaimSuspendedPaymentBlocker.cs`
   - **File:** `src/Infrastructure/Payments/EventHandlers/ClaimSuspendedPaymentBlocker.cs`
   - **Subscribes to:** `ClaimSuspendedDomainEvent`
   - **Actions:**
     - Query all payments linked to the suspended claim
     - Filter payments with status `Pending`, `Processing`, or `Scheduled`
     - Update payment status to `Blocked`
     - Add block reason: "Claim suspended - fraud investigation"
     - Log blocking action for audit trail

2. **Payment Sync Service Impact:**
   - **File:** `src/Infrastructure/Payments/Services/ClaimPaymentSyncService.cs`
   - **Modification:** Add check to skip blocked payments during sync operations
   - **Rule:** Payments with status `Blocked` must never be sent to payment gateway

3. **Database Changes:**
   - Add `PaymentBlockReason` column to Payments table (nvarchar(500), nullable)
   - Add `BlockedAt` timestamp column to Payments table (datetime2, nullable)
   - Create index on `ClaimId` + `Status` for faster blocking queries

### 3.2 Test Scenarios

#### Unit Tests

**Test File:** `tests/Domain/ClaimTests/ClaimSuspensionTests.cs`

**Test Cases:**
1. `Should_SuspendClaim_When_ClaimIsActive`
   - Given: Claim with status "Active"
   - When: Suspend() is called with valid reason
   - Then: Status becomes "Suspended", domain event is raised

2. `Should_ThrowException_When_SuspendingAlreadySuspendedClaim`
   - Given: Claim with status "Suspended"
   - When: Suspend() is called again
   - Then: InvalidClaimStateException is thrown

3. `Should_ThrowException_When_SuspendingClosedClaim`
   - Given: Claim with status "Closed" or "Paid"
   - When: Suspend() is called
   - Then: InvalidClaimStateException is thrown

4. `Should_RequireValidSuspensionReason`
   - Given: Claim in active state
   - When: Suspend() is called with reason < 10 characters
   - Then: ArgumentException is thrown

**Test File:** `tests/Application/Claims/Commands/SuspendClaimCommandTests.cs`

**Test Cases:**
1. `Should_SuspendClaim_When_CommandIsValid`
2. `Should_FailValidation_When_ReasonIsTooShort`
3. `Should_FailValidation_When_UserIsUnauthorized`
4. `Should_PublishDomainEvent_AfterSuccessfulSuspension`

#### Integration Tests

**Test File:** `tests/Integration/Payments/ClaimPaymentSyncTests.cs`

**Critical Test Cases:**

1. `Should_BlockAllPendingPayments_When_ClaimIsSuspended`
   - **Given:** 
     - A claim with status "Active"
     - 3 payments associated with the claim:
       - Payment A: Status = "Pending"
       - Payment B: Status = "Processing"
       - Payment C: Status = "Completed"
   - **When:** Claim is suspended via command
   - **Then:**
     - Payment A status changes to "Blocked"
     - Payment B status changes to "Blocked"
     - Payment C status remains "Completed" (no change)
     - All blocked payments have `PaymentBlockReason` populated
     - Domain events are published for each blocked payment

2. `Should_PreventPaymentSync_When_PaymentIsBlocked`
   - **Given:**
     - A suspended claim
     - A payment with status "Blocked"
   - **When:** Payment sync service runs
   - **Then:**
     - Blocked payment is NOT sent to payment gateway
     - Sync log shows "Skipped: Payment blocked due to claim suspension"
     - No financial transaction occurs

3. `Should_MaintainDataConsistency_AfterSuspension`
   - **Given:** A claim with multiple associated entities (payments, documents, notes)
   - **When:** Claim is suspended
   - **Then:**
     - All related data remains intact (no data loss)
     - Suspension is reflected in read models within 1 second
     - Audit trail captures the suspension event

#### Functional Tests

**Test File:** `tests/Functional/Claims/ClaimSuspensionWorkflowTests.cs`

**End-to-End Scenarios:**

1. **Happy Path - Fraud Detection**
   - User with ClaimsManager role detects suspicious activity
   - User calls SuspendClaim API with reason "Suspected fraudulent documentation"
   - System suspends claim immediately
   - All pending payments are blocked within 5 seconds
   - Notification is sent to fraud investigation team
   - Claimant receives communication about claim review (without mentioning fraud)

2. **Edge Case - Race Condition**
   - Payment is being processed at the exact moment claim is suspended
   - System must handle concurrent suspension and payment processing
   - Either payment completes before suspension OR payment is blocked
   - No partial states or data corruption

3. **Authorization Failure**
   - User without proper role attempts to suspend claim
   - System returns 403 Forbidden
   - No changes to claim or payments
   - Security event is logged

---

## Implementation Phases

### Phase 1: Domain Foundation (Priority: High)
- [ ] Add `Suspended` to `ClaimStatus.cs`
- [ ] Implement `Suspend()` method in `Claim.cs`
- [ ] Create `ClaimSuspendedDomainEvent`
- [ ] Write domain unit tests

### Phase 2: Application Layer (Priority: High)
- [ ] Create `SuspendClaimCommand`
- [ ] Implement command handler
- [ ] Add validation rules
- [ ] Write application tests

### Phase 3: Payment Integration (Priority: Critical)
- [ ] Implement `ClaimSuspendedPaymentBlocker` event handler
- [ ] Update `ClaimPaymentSyncService` to respect blocked status
- [ ] Database migration for payment blocking fields
- [ ] Write integration tests

### Phase 4: Testing & Validation (Priority: High)
- [ ] Complete all unit tests
- [ ] Execute integration test suite
- [ ] Perform functional testing
- [ ] Security review and penetration testing

---

## Risk Assessment

**High Risk:**
- Payment blocking failure could result in financial loss
- Race conditions between suspension and payment processing

**Mitigation:**
- Implement idempotent blocking operations
- Use database transactions for claim suspension + payment blocking
- Add monitoring alerts for blocked payment failures

**Medium Risk:**
- Performance impact on payment sync service
- Increased database load from blocking queries

**Mitigation:**
- Index optimization on ClaimId + Status
- Async processing for payment blocking
- Batch operations for multiple payments

---

## Success Criteria

1. **Functional:**
   - Claim can be suspended by authorized users
   - All pending payments are blocked within 5 seconds
   - No payments are processed for suspended claims

2. **Technical:**
   - Clean Architecture layers remain isolated
   - No regression in existing tests
   - Code coverage > 80% for new code

3. **Business:**
   - Fraud investigation team can effectively suspend claims
   - Financial exposure during investigation is minimized
   - Audit trail supports compliance requirements

---

## Files to Create/Modify

### New Files:
- `src/Domain/Events/ClaimSuspendedDomainEvent.cs`
- `src/Application/Claims/Commands/SuspendClaimCommand.cs`
- `src/Application/Claims/Commands/SuspendClaimCommandHandler.cs`
- `src/Application/Claims/Commands/SuspendClaimCommandValidator.cs`
- `src/Infrastructure/Payments/EventHandlers/ClaimSuspendedPaymentBlocker.cs`
- `tests/Domain/ClaimTests/ClaimSuspensionTests.cs`
- `tests/Application/Claims/Commands/SuspendClaimCommandTests.cs`
- `tests/Integration/Payments/ClaimPaymentSyncTests.cs`
- `tests/Functional/Claims/ClaimSuspensionWorkflowTests.cs`

### Modified Files:
- `src/Domain/Enums/ClaimStatus.cs`
- `src/Domain/Entities/Claim.cs`
- `src/Infrastructure/Payments/Services/ClaimPaymentSyncService.cs`
- Database schema (migration script)

---

## Notes for Copilot

When implementing this plan:
1. Follow existing code patterns in the codebase
2. Use dependency injection for all services
3. Apply SOLID principles throughout
4. Ensure all public methods have XML documentation
5. Use the existing logging framework (Serilog)
6. Follow the established error handling patterns
7. Maintain backward compatibility with existing APIs
8. Update API documentation (Swagger/OpenAPI) as needed

**DO NOT** modify files outside the specified scope without explicit approval.
**DO NOT** break existing functionality while implementing new features.
**ALWAYS** write tests before or alongside implementation code.