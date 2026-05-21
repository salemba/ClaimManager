# Role
Senior DDD & Full-Stack Developer (.NET 10 / React 19).

# Context
- User Story: #3-3-detect-workload-imbalance-and-recurring-blocker-patterns.md (Urgent Business Request section)
- Domain Entity: `src/ClaimManager.Domain/Claims/Entities/Claim.cs`
- Application Command: `src/ClaimManager.Application/Claims/Commands/InterveneOnClaimCommand.cs`
- API Endpoint: `src/ClaimManager.Api/Endpoints/Claims/ClaimEndpoints.cs`
- Frontend UI: `src/ClaimManager.Frontend/src/features/claims/components/WorkflowActionsPanel.tsx`
- Frontend API: `src/ClaimManager.Frontend/src/features/claims/api/claimsApi.ts`

# Task
Implement the "Supervisor Intervention" feature as defined in the Urgent Business Request of Story 3.3. This allows a supervisor to manually reassign a claim and force its transition to a higher workflow state.

# Requirements & Rules
1. **Business Logic (Domain):**
   - Add a `Intervene(string newOwnerId, string targetStatus, string supervisorId)` method to `Claim.cs`.
   - **Enforce Intervention Rules:** The action is only allowed if:
     - The claim has been blocked for > 48 hours (Note: You must add `BlockedAtUtc` to `Claim` and ensure it's set when `BlockerType` changes from `null` to non-null).
     - **OR** its amount (`PaymentAmount`) exceeds €10,000.
   - Automatically record the intervention details (original state/owner -> new state/owner) as a `ClaimNote`.

2. **Backend (Application/API):**
   - Create `InterveneOnClaimCommand` and its corresponding handler.
   - Expose a `POST /api/claims/{id}/intervene` endpoint.
   - Secure the endpoint with the `Supervisor` policy.

3. **Frontend (React):**
   - In `WorkflowActionsPanel.tsx`, add an "Intervention" button.
   - Visibility: Only visible to users with the `Supervisor` role.
   - Availability: Only enabled if the claim meets the intervention criteria (blocked > 48h or amount > €10k).
   - Action: Open a modal to select the new `AdjusterId` and `TargetStatus`.

# Constraint
- Do not bypass the Domain layer; all logic for "can intervene" and state modification must reside in `Claim.cs`.
- Ensure the transition is recorded in the audit trail (Claim Notes).
- Follow existing patterns for Command/Handler and API Endpoint implementation.

# Output
- description.md file summarizing the technical implementation.
