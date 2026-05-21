I am updating the 3-3-detect-workload-imbalance-and-recurring-blocker-patterns.md file to incorporate the new supervisor intervention requirements. This includes adding a new acceptance criterion, a dedicated task list, and updating the scope guardrails to reflect that these write operations are now within the scope of this story.

// ...existing code...
3. Given the same data supports both claim-level and pattern-level decisions, when the supervisor switches between those levels, then the system keeps the transition understandable, and does not lose the operational context that explains why the pattern matters.

4. Given the user has the 'Supervisor' role, when a claim is older than 48 hours OR the claim amount is > 10,000, then the supervisor is permitted to change the `adjusterId` and force the workflow state, with every such intervention recorded in the immutable audit trail including the supervisor's identity and a mandatory reason for the action.

## Tasks / Subtasks
// ...existing code...
- [x] Run `dotnet test tests/ClaimManager.Api.FunctionalTests`, `dotnet test tests/ClaimManager.Application.UnitTests`, `npm test` in `tests/ClaimManager.Frontend.Tests`, and `npm run build` in `src/ClaimManager.Frontend` to confirm no regressions.

- [ ] Implement supervisor intervention (reassignment and state override). (AC: 4)
  - [ ] Create `SupervisorInterventionDto(string NewAdjusterId, string? NewState, string Reason)` in `src/ClaimManager.Application/Claims/Dtos/SupervisorInterventionDto.cs`.
  - [ ] Add `InterveneAsync(Guid claimId, SupervisorInterventionDto intervention)` to the Application layer (e.g., `IClaimService`).
  - [ ] Implement validation logic: ensure requester is a Supervisor and claim meets thresholds (Age > 48h OR Amount > 10,000).
  - [ ] Pipeline the intervention into the `AuditTrail` system, ensuring the reason and supervisor ID are persisted.
  - [ ] Expose the intervention via `POST /api/claims/{id}/intervene` in `src/ClaimManager.Api/Endpoints/Claims/ClaimEndpoints.cs`.
  - [ ] Add an "Intervene" button/dialog to the frontend `ClaimDetail` view, visible only to supervisors when criteria are met.

## Dev Notes
// ...existing code...
### Scope Guardrails

**In scope:**
- Read-only workload distribution across claim owners (per-owner metrics: total, stuck, aging, blocker counts)
- Enriched blocker pattern context (owner spread, aging prevalence per blocker type)
- Navigation from workload and pattern views to claims queue with preserved dashboard context
- Backend DTO extensions and frontend component additions within the existing `features/dashboard` module
- Supervisor intervention: reassignment and workflow state override for claims meeting aging (>48h) or amount (>10,000) thresholds.

**Out of scope:**
- Advanced escalation workflows or multi-stage approval overrides — that is Story 3.4
- User name resolution or a user directory lookup — use `OwnedByUserId` string as-is (consistent with existing `ClaimPreviewRow` which already renders `claim.ownedByUserId`)
// ...existing code...

Analysis Document

# Analysis: Supervisor Intervention Requirements Integration

## 1. Requirement Summary
The urgent requirement introduces a "break-glass" capability for supervisors. It allows them to bypass standard workflow constraints for claims that are either stalled (older than 48 hours) or of high financial significance (amount > 10,000). The specific actions allowed are changing the assigned adjuster (`adjusterId`) and forcing a transition to a new workflow state.

## 2. Impact Analysis
### Security & RBAC
- **Role Enforcement**: The system must strictly verify the `Supervisor` role at the API level before executing the intervention.
- **Threshold Validation**: The backend must independently verify the claim age or amount before allowing the state/owner modification to prevent unauthorized overrides.

### Auditability
- **Immutable Trail**: As per the PRD's mandate for enterprise integrity, this intervention must be logged to an immutable audit trail. This ensures that "super-user" actions are transparent and can be reviewed by IT security or product owners.
- **Reasoning**: A mandatory `Reason` field is included in the DTO to capture the justification for the intervention, which is critical for future workload imbalance analysis.

### Operational Metrics
- **Reducing Stuck Claims**: This feature directly supports the goal of a 30% reduction in stuck claims by providing a mechanism to resolve bottlenecks that automated or standard processes cannot handle.
- **Cycle Time**: Enabling faster reassignment for high-value or aging claims will improve the overall claim cycle time.

## 3. Technical Implementation Strategy
- **Application Layer**: A new DTO and service method are proposed to handle the logic. This keeps the domain models clean while centralizing the business rules for intervention.
- **API Layer**: A dedicated endpoint `POST /api/claims/{id}/intervene` follows RESTful patterns for an action-based transition.
- **Frontend Layer**: The "Intervene" UI should be contextual. It should only be rendered when the claim state and user permissions satisfy the 48h or >10k rules, maintaining the "high desktop information density" and "workflow legibility" design principles.
