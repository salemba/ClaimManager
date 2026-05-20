# Architectural Review: Suspend Claim for Fraud Suspicion

## Overall Assessment

The plan is **above average for a feature-level design doc**. Domain modeling is sound, the layering is disciplined, and the test coverage is unusually thorough. However, there are meaningful gaps — particularly around the `LiftSuspension` counterpart, the payment gateway integration, concurrency, and observability — that carry real delivery risk.

---

## Critical Findings

### 1. `LiftSuspensionCommand` is declared in-scope but entirely unspecified

**Severity: High**

The Appendix (Decision 1) confirms that `LiftSuspensionCommand` must ship in the same sprint. Yet the plan contains zero design for it — no domain method spec, no handler workflow, no test scenarios, no event definition.

`LiftSuspension` is not simpler than `Suspend`. It has its own non-trivial invariants:

- What happens to payments that were `Blocked` because they were already dispatched to the gateway and cancelled? Do they return to `Pending`, or do they require manual review before reactivation?
- Does `LiftSuspension` require the same escalated authorization path as `Approved → Suspended`?
- What is the `ClaimSuspensionLiftedDomainEvent` payload, and does the payment sync pipeline need a corresponding change to resume processing?

**Recommendation:** Either write the full `LiftSuspension` spec before sprint start, or formally descope it with a documented reason. Shipping `Suspend` without `Lift` delivers a one-way door — claims enter a suspended state with no defined exit path. That is a product risk, not just a technical debt item.

---

### 2. `PaymentGatewayRecallHandler` is in-scope but critically underspecified

**Severity: High**

Decision 3 confirms this handler is mandatory, yet the plan gives it three sentences. For an external integration involving money, this is insufficient. Missing entirely:

- **What is the gateway's cancellation API contract?** Can it be called with a list of payment IDs, or must it be called per-payment?
- **What constitutes "already dispatched but not yet settled"?** This boundary depends on the gateway's own state model. The plan does not define which payment statuses on the gateway side are recallable.
- **Retry and dead-letter policy** is mentioned but explicitly deferred to "the infrastructure team" — in a plan where the handler is in-scope for this sprint, that deferral is a blocker.
- **Partial failure behavior:** If the handler successfully cancels 3 of 5 dispatched payments and then the gateway times out, what is the recovery path? The plan says the handler must be idempotent but does not define how idempotency is implemented (e.g., checking gateway status before issuing a recall, storing a local record of sent recalls).

**Recommendation:** Add a `PaymentGatewayRecallHandler` section with gateway API contract, idempotency key strategy, retry policy (with specific backoff and max-attempt values), dead-letter destination, and a reconciliation/alerting trigger for payments that could not be recalled.

---

### 3. The race condition mitigation is incomplete

**Severity: Medium**

Section 3.1.3 identifies the race condition between an in-flight sync job and a suspension commit, and correctly proposes a processor-level re-check as mitigation. However, it dismisses a distributed lock as unnecessary. That conclusion is only safe if the re-check uses **optimistic concurrency control** (row-level version/etag), which the plan does not specify.

Without a version check, the following sequence is still possible:

1. Sync job reads payment as `Pending`, stores it in memory.
2. Suspension commits, payment is now `Blocked` in DB.
3. Sync job re-checks payment status — reads `Blocked` — skips correctly. ✓

This specific case is handled. But what about:

1. Two concurrent suspension commands arrive for the same claim.
2. Both load the aggregate (both see `InReview`).
3. Both call `Suspend()` — both pass the "already suspended" guard on their in-memory copy.
4. Both try to persist. One overwrites the other silently if there is no concurrency token.

**Recommendation:** The plan must specify that the `Claim` aggregate uses a concurrency token (ETag / row version). The repository `Save` must perform an optimistic concurrency check. The handler must handle the resulting concurrency exception and return an appropriate result (retry or conflict error).

---

### 4. Authorization is checked before the aggregate is loaded — but escalated auth requires the aggregate

**Severity: Medium**

The handler workflow (Section 2.3) is: Validate → Authorize → Load aggregate → Invoke domain.

Decision 2 specifies that if the loaded claim is in `Approved` status, the actor must hold `SuspendApprovedClaim` permission and must have set `EscalatedConfirmation = true`. This means the authorization check is **split across two steps** — standard role check before load, escalated permission check after load — but the plan presents the flow as a single linear sequence that only mentions authorization once (Step 2).

This is not reflected in the test scenarios. Scenario 13 only tests the standard role check. There is no test scenario for the case where a Claims Manager with standard permissions attempts to suspend an `Approved` claim without the elevated permission.

**Recommendation:** The handler workflow must explicitly show a Step 4a (post-load authorization branch) for `Approved`-status claims. Add test scenarios: (a) actor has `SuspendClaim` but not `SuspendApprovedClaim` on an `Approved` claim → `Forbidden`; (b) actor has both permissions but `EscalatedConfirmation` is `false` → `Forbidden`; (c) actor has both and flag is `true` → proceeds.

---

### 5. Domain event dispatch strategy has an implicit reliability assumption

**Severity: Medium**

The plan correctly places event dispatch after the DB commit (Step 6), to avoid notifying on rolled-back transactions. However, it does not address the gap between "transaction committed" and "event dispatched":

- If the process crashes or is killed after the commit but before dispatch, the `ClaimSuspendedDomainEvent` is never sent. Payments are blocked in the DB, but the gateway recall handler never fires and stakeholders are never notified.
- The plan offers no recovery path for this scenario.

This is the classic dual-write problem. The plan's current approach (in-memory dispatch post-commit) gives at-most-once delivery.

**Recommendation:** For a financial-grade feature involving external gateway calls, commit the events to an outbox table within the same DB transaction, and use a background worker to dispatch them. This is a standard pattern (Transactional Outbox) that gives at-least-once delivery with idempotent consumers. If the team has already decided against this pattern, that decision should be documented explicitly with the accepted risk.

---

### 6. No migration or deployment strategy

**Severity: Medium**

The plan adds a new `ClaimStatus` enum value, three new columns on the `Claim` table, a new `PaymentStatus.Blocked` value, and new columns on the `Payment` table. None of this is addressed:

- Is this a backwards-compatible migration? Can old application instances (pre-deploy) read a claim that a new instance has suspended?
- Is there a blue/green or rolling deployment in use? If so, the old code must not crash when it reads an unknown status value from the DB.
- Are there existing `switch` statements or conditionals on `ClaimStatus` elsewhere in the codebase that will fall through to a default case for `Suspended`? A code audit of all `ClaimStatus` consumers is a required pre-delivery step.

**Recommendation:** Add a deployment section that covers: migration script (additive columns with nullable defaults), enum exhaustiveness audit across the codebase, and a rollback script (what happens if the feature needs to be reverted after the migration runs).

---

## Moderate Findings

### 7. `FraudSuspicionReason` is free-text but the plan is inconsistent about this

Section 1.2.1 defines it as "free-text **or enumerated**." Section 2.1 types it as `String`. The test scenarios treat it as a plain string. If it is ever enumerated, the validation rules, DB column type, and notification content all change significantly. The ambiguity should be resolved and the word "or enumerated" removed if free-text is confirmed.

### 8. Notification content for the claimant is underspecified

Decision 5 specifies that the claimant receives a "generic 'under review' message." This introduces a regulatory dimension that the plan does not mention. In many insurance jurisdictions, a claims manager is legally required to notify a claimant of adverse action with specific disclosures within a mandated timeframe. The plan should flag this as requiring legal/compliance review before the notification handler is built — it is not purely a technical decision.

### 9. No observability requirements beyond sync pipeline metrics

The sync pipeline section specifies `processed / skipped_blocked / failed` metrics, which is good. But there are no observability requirements for the command path itself: no mention of structured logging for the suspension command (who suspended what, when, with what reason), no mention of a metric or alert for a spike in suspension rate (which could indicate a bug or an abuse pattern), and no mention of tracing spans for the gateway recall handler.

### 10. `Completed` payment behavior in the suspended claim is undefined beyond tests

Scenario 9 correctly tests that `Completed` payments are not blocked. But the plan does not state what happens if a payment that was `Completed` at suspension time is later found to have been fraudulent. Is there a clawback flow? This is out of scope for this story, but it should be explicitly called out as a known gap so the next sprint does not discover it by accident.

---

## Structural Positives (Worth Preserving)

- The three-layer validation separation (input → authorization → domain invariants) is correct and well-reasoned.
- Placing the `BlockedPaymentIds` in the domain event payload to avoid re-querying is a good design choice.
- The processor-level defense-in-depth in the sync pipeline is the right approach.
- Test scenario coverage at the domain unit test layer is comprehensive.
- The Appendix pattern for documenting resolved architectural decisions is excellent and should be kept.

---

## Priority Action Items

| Priority | Finding | Action Required |
|---|---|---|
| P0 | `LiftSuspensionCommand` unspecified | Write full spec before sprint start or formally descope |
| P0 | Gateway recall handler underspecified | Define contract, retry policy, partial failure behavior |
| P1 | No optimistic concurrency control | Add version token to aggregate + handler concurrency exception handling |
| P1 | Split authorization logic not reflected in tests | Add post-load auth step to workflow + 3 missing test scenarios |
| P1 | At-most-once event delivery | Evaluate Transactional Outbox; document decision either way |
| P1 | No migration/deployment strategy | Add DB migration script, rollback plan, enum exhaustiveness audit |
| P2 | Notification legal/compliance gap | Flag for compliance review before building notification handler |
| P2 | Missing command-path observability | Define structured logging and metrics for suspension command |