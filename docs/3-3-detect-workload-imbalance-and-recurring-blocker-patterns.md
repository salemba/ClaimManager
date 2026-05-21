---
title: "Detect Workload Imbalance and Recurring Blocker Patterns"
story_key: 3-3-detect-workload-imbalance-and-recurring-blocker-patterns
epic: 3 - Supervisor Oversight and Intervention
status: review
assignee:
created: 2026-05-17
---

# Story 3.3: Detect Workload Imbalance and Recurring Blocker Patterns

Status: review


## Story

As a supervisor,
I want to see uneven workload distribution and recurring blocker patterns,
so that I can intervene at the team and workflow level instead of only reacting claim by claim.

## Acceptance Criteria

1. Given multiple claims are assigned across adjusters or teams, when the supervisor reviews workload views, then the system highlights meaningful imbalance in claim load, aging pressure, or unresolved blockers, and the presentation supports practical redistribution decisions.

2. Given blocker patterns recur across claims or queues, when the dashboard analyzes the current operational picture, then recurring blocker types are surfaced as recognizable patterns, and the supervisor can tell where systemic intervention may have more impact than single-claim action.

3. Given the same data supports both claim-level and pattern-level decisions, when the supervisor switches between those levels, then the system keeps the transition understandable, and does not lose the operational context that explains why the pattern matters.

4. Given the user has the 'Supervisor' role, when a claim is older than 48 hours OR the claim amount is > 10,000, then the supervisor is permitted to change the `adjusterId` and force the workflow state, with every such intervention recorded in the immutable audit trail including the supervisor's identity and a mandatory reason for the action. (AC: 4)

## Tasks / Subtasks

- [x] Extend the dashboard DTO layer with workload and pattern types. (AC: 1, 2)
  - [x] Add `WorkloadOwnerSummaryDto(string OwnerId, int TotalCount, int StuckCount, int AgingCount, int BlockerCount)` to `src/ClaimManager.Application/Dashboard/Dtos/SupervisorDashboardDto.cs`.
  - [x] Extend `BlockerGroupSummaryDto` with two new fields — `int AffectedOwnerCount` and `int AgingClaimCount` — to carry systemic pattern context. Keep `BlockerType` and `Count` positionally first so any existing DTO construction in tests that passes positional arguments fails loudly if not updated.
  - [x] Extend `SupervisorDashboardDto` with a new `IReadOnlyList<WorkloadOwnerSummaryDto> WorkloadDistribution` property appended after the existing four properties.

- [x] Compute workload distribution and enriched blocker patterns in the backend. (AC: 1, 2)
  - [x] In `src/ClaimManager.Api/Endpoints/Dashboard/DashboardEndpoints.cs`, after the existing `normalizedClaims` projection, group claims by `OwnedByUserId` (treat `null` as `"unassigned"`) and compute per-owner: `TotalCount`, `StuckCount` (has a non-null `BlockerType`), `AgingCount` (created before `agingCutoff`), `BlockerCount` (same as `StuckCount` for now — separated for future extension). Order owners descending by `StuckCount + AgingCount` so highest-burden owners appear first.
  - [x] Enrich the existing `blockerSummary` computation to include `AffectedOwnerCount` (count of distinct non-null `OwnedByUserId` values within that blocker group) and `AgingClaimCount` (count of blocked claims whose `CreatedAtUtc` is before `agingCutoff`). The existing ordering (descending by `Count`) is preserved.
  - [x] Pass the new `WorkloadDistribution` list as the fifth argument when constructing `SupervisorDashboardDto` — keep the return type `Ok<SupervisorDashboardDto>` unchanged.

- [x] Update the frontend API contract to match the new backend shape. (AC: 1, 2)
  - [x] In `src/ClaimManager.Frontend/src/features/dashboard/api/dashboardApi.ts`, add `WorkloadOwnerSummary` interface: `{ ownerId: string; totalCount: number; stuckCount: number; agingCount: number; blockerCount: number }`.
  - [x] Extend `BlockerGroupSummary` interface with `affectedOwnerCount: number` and `agingClaimCount: number`.
  - [x] Extend `SupervisorDashboard` interface with `workloadDistribution: WorkloadOwnerSummary[]`.

- [x] Add a workload distribution section to the supervisor dashboard. (AC: 1, 3)
  - [x] In `src/ClaimManager.Frontend/src/features/dashboard/components/SupervisorDashboard.tsx`, add a `WorkloadDistributionSection` component that renders a list of adjuster rows. Each row shows: owner ID, total claim count, stuck count (color-coded error when > 0), aging count (color-coded warning when > 0). Keep the row compact and desktop-dense.
  - [x] Each owner row must navigate to `/claims?ownedByUserId=<ownerId>` with `state: { dashboardOrigin: { label: 'Workload: <ownerId>', backTo: '/' } }` on click — exactly matching the `dashboardOrigin` pattern established in Stories 3.1 and 3.2.
  - [x] Render `WorkloadDistributionSection` between the existing `BlockerSummarySection` and the high-risk/aging two-column grid. Only render when `workloadDistribution.length > 0`.
  - [x] Do not render the workload section at all when `workloadDistribution` is empty or contains only one owner — a single owner means there is nothing to compare and no imbalance to surface.

- [x] Enrich the existing blocker summary section with systemic pattern context. (AC: 2, 3)
  - [x] In `BlockerSummarySection` inside `SupervisorDashboard.tsx`, upgrade each `Chip` tooltip or subtitle to show systemic weight: if `affectedOwnerCount > 1`, append ` — ${affectedOwnerCount} adjusters` to the chip label. If `agingClaimCount > 0`, render a secondary caption "includes aging" beneath the chip in `warning.main` color. Use MUI `Tooltip` wrapping the `Chip` to display full context without expanding the chip width.
  - [x] The chip's `onClick` navigation remains unchanged: `/claims?blockerType=...&hasBlocker=true` with `dashboardOrigin` state.

- [x] Add regression and new validation tests. (AC: 1, 2, 3)
  - [x] In `tests/ClaimManager.Api.FunctionalTests`, extend `SupervisorDashboardEndpointTests.cs` to assert: `workloadDistribution` is present in the response, the field is an array, and a multi-owner scenario returns owners with correct `stuckCount` and `agingCount`. Reuse the existing supervisor/admin auth helper.
  - [x] Assert that `blockerSummary` entries now include `affectedOwnerCount` and `agingClaimCount` with correct values.
  - [x] In `tests/ClaimManager.Frontend.Tests/src/features/dashboard/supervisorDashboard.test.tsx`, add tests for: workload section renders when multiple owners present, workload section is hidden when one or zero owners present, owner row navigation fires with correct path and `dashboardOrigin` state, blocker chip tooltip shows "adjusters" text when `affectedOwnerCount > 1`.
  - [x] Run `dotnet test tests/ClaimManager.Api.FunctionalTests`, `dotnet test tests/ClaimManager.Application.UnitTests`, `npm test` in `tests/ClaimManager.Frontend.Tests`, and `npm run build` in `src/ClaimManager.Frontend` to confirm no regressions.

- [ ] Implement supervisor intervention (reassignment and state override). (AC: 4)
  - [ ] Create `SupervisorInterventionDto(string NewAdjusterId, string? NewState, string Reason)` in `src/ClaimManager.Application/Claims/Dtos/SupervisorInterventionDto.cs`.
  - [ ] Add `InterveneAsync(Guid claimId, SupervisorInterventionDto intervention)` to the Application layer (e.g., `IClaimService`).
  - [ ] Implement validation logic: ensure requester is a Supervisor and claim meets thresholds (Age > 48h OR Amount > 10,000).
  - [ ] Pipeline the intervention into the `AuditTrail` system, ensuring the reason and supervisor ID are persisted.
  - [ ] Expose the intervention via `POST /api/claims/{id}/intervene` in `src/ClaimManager.Api/Endpoints/Claims/ClaimEndpoints.cs`.
  - [ ] Add an "Intervene" button/dialog to the frontend `ClaimDetail` view, visible only to supervisors when criteria are met.

## Dev Notes

### Story Intent

Story 3.3 adds two analytical dimensions to the supervisor dashboard that Story 3.1 deliberately deferred: per-owner workload distribution and systemic blocker pattern context. The dashboard already computes claim-level risk signals. This story layers on team-level awareness and targeted intervention capability so a supervisor can act at workload scale and resolve stalled high-value claims.

The correct implementation posture is additive and disciplined:
- extend existing DTOs rather than replacing them,
- compute workload groupings server-side from the same in-memory `normalizedClaims` array already loaded by the endpoint,
- reuse the `dashboardOrigin` navigation context pattern established in Stories 3.1 and 3.2,
- introduce targeted reassignment and state override for stalled (>48h) or high-value (>10k) claims, strictly audited.
- Supervisor intervention: reassignment and workflow state override for claims meeting aging (>48h) or amount (>10,000) thresholds.

**Out of scope:**
- Advanced escalation workflows or multi-stage approval override
**In scope:**
- Read-only workload distribution across claim owners (per-owner metrics: total, stuck, aging, blocker counts)
- Enriched blocker pattern context (owner spread, aging prevalence per blocker type)
- Navigation from workload and pattern views to claims queue with preserved dashboard context
- Backend DTO extensions and frontend component additions within the existing `features/dashboard` module

**Out of scope:**
- Reassignment, escalation commands, or any write operation on claims — that is Story 3.4
- User name resolution or a user directory lookup — use `OwnedByUserId` string as-is (consistent with existing `ClaimPreviewRow` which already renders `claim.ownedByUserId`)
- Historical trend analysis or time-series blocker recurrence — the story is about current operational state, not historical patterns
- New background jobs, caching layers, or event-driven updates — the existing 60-second refetch cadence in `SupervisorDashboard.tsx` covers freshness needs
- Any changes to the drill-down flow established in Story 3.2

### Root Cause Context

After Story 3.2, the dashboard shows risk signals and supports claim-level drill-down. However, two gaps remain for team-level oversight:

1. **No workload visibility:** The dashboard has no view of how claims are distributed  and `src/ClaimManager.Application/Claims/Dtos/SupervisorInterventionDto.cs`
- API implementation: `src/ClaimManager.Api/Endpoints/Dashboard/DashboardEndpoints.cs` and `src/ClaimManager.Api/Endpoints/Claims/ClaimEndpoints.cs`
- Frontend API contract: `src/ClaimManager.Frontend/src/features/dashboard/api/dashboardApi.ts`
- Frontend component: `src/ClaimManager.Frontend/src/features/dashboard/components/SupervisorDashboard.tsx` and `src/ClaimManager.Frontend/src/features/claims/components/ClaimDetail
Both gaps are solvable from the data already loaded by `GetSupervisorDashboardAsync` — no new database queries are needed. The `normalizedClaims` array contains `OwnedByUserId`, `BlockerType`, `CreatedAtUtc`, and `HasDataIntegrityWarning` for every active claim.

### Architecture Compliance

Architecture-defined locations that this story targets — no deviation:
- Backend DTOs: `src/ClaimManager.Application/Dashboard/Dtos/SupervisorDashboardDto.cs`
- API implementation: `src/ClaimManager.Api/Endpoints/Dashboard/DashboardEndpoints.cs`
- Frontend API contract: `src/ClaimManager.Frontend/src/features/dashboard/api/dashboardApi.ts`
- Frontend component: `src/ClaimManager.Frontend/src/features/dashboard/components/SupervisorDashboard.tsx`

Do not:
- Add dashboard logic to `WorkspaceEndpoints.cs` — workspace is adjuster-oriented
- Store workload state in Zustand — TanStack Query manages all server state
- Re-query the database separately for workload distribution — compute from `normalizedClaims` in-memory
- Create a new `/api/workload` endpoint — this is an extension of the existing supervisor dashboard response

### Existing Files This Story Will Modify

#### `src/ClaimManager.Application/Dashboard/Dtos/SupervisorDashboardDto.cs`
**Current state:** Defines `SupervisorDashboardSignals(StuckCount, AgingCount, AttentionRequiredCount, ApprovalPressureCount)`, `BlockerGroupSummaryDto(BlockerType, Count)`, `DashboardClaimPreviewDto(...)`, and `SupervisorDashboardDto(Signals, BlockerSummary, HighRiskClaims, AgingClaims, GeneratedAtUtc)`.
**What this story changes:** Add `WorkloadOwnerSummaryDto` record. Extend `BlockerGroupSummaryDto` with `AffectedOwnerCount` and `AgingClaimCount`. Extend `SupervisorDashboardDto` with `WorkloadDistribution`.
**What must be preserved:** All existing properties and their types. The `SupervisorDashboardDto` positional record constructor must remain consistent with every call site in `DashboardEndpoints.cs` and in functional tests.

#### `src/ClaimManager.Api/Endpoints/Dashboard/DashboardEndpoints.cs`
**Current state:** Fetches all active claims (non-terminal statuses) in one `ToArrayAsync` call. Projects to `ActiveClaim` records. Computes signals, blocker summary, high-risk claims, and aging claims in-memory. Returns `Ok<SupervisorDashboardDto>`.

```
Current blockerSummary computation (lines 84-90):
  var blockerSummary = normalizedClaims
      .Where(c => c.BlockerType is not null)
      .GroupBy(c => c.BlockerType!)
      .Select(g => new BlockerGroupSummaryDto(g.Key, g.Count()))
      .OrderByDescending(g => g.Count)
      .ThenBy(g => g.BlockerType)
      .ToArray();
```

**What this story changes:** Extend `blockerSummary` projection to compute `AffectedOwnerCount` and `AgingClaimCount` within each group. Add `workloadDistribution` computation after the existing signal/blocker computations. Pass `workloadDistribution` to the `SupervisorDashboardDto` constructor.
**What must be preserved:** The existing query fetches a single `ToArrayAsync` call — this story must NOT add additional database queries. All new computation stays in-memory from `normalizedClaims`. The `GetRiskScore` and `NormalizeBlockerType` helpers are unchanged.

Concrete extension pattern for `blockerSummary`:
```csharp
var blockerSummary = normalizedClaims
    .Where(c => c.BlockerType is not null)
    .GroupBy(c => c.BlockerType!)
    .Select(g => new BlockerGroupSummaryDto(
        g.Key,
        g.Count(),
        g.Select(c => c.OwnedByUserId).Distinct().Count(),   // AffectedOwnerCount
        g.Count(c => c.CreatedAtUtc < agingCutoff)))          // AgingClaimCount
    .OrderByDescending(g => g.Count)
    .ThenBy(g => g.BlockerType)
    .ToArray();
```

Concrete `workloadDistribution` computation pattern:
```csharp
var workloadDistribution = normalizedClaims
    .GroupBy(c => c.OwnedByUserId ?? "unassigned")
    .Select(g => new WorkloadOwnerSummaryDto(
        g.Key,
        g.Count(),
        g.Count(c => c.BlockerType is not null),              // StuckCount
        g.Count(c => c.CreatedAtUtc < agingCutoff),           // AgingCount
        g.Count(c => c.BlockerType is not null)))             // BlockerCount (= StuckCount for now)
    .OrderByDescending(o => o.StuckCount + o.AgingCount)
    .ThenByDescending(o => o.TotalCount)
    .ToArray();
```

#### `src/ClaimManager.Frontend/src/features/dashboard/api/dashboardApi.ts`
**Current state:** Defines `SupervisorDashboardSignals`, `BlockerGroupSummary`, `DashboardClaimPreview`, `SupervisorDashboard` interfaces and `getSupervisorDashboard` function.
**What this story changes:** Add `WorkloadOwnerSummary` interface. Add `affectedOwnerCount` and `agingClaimCount` to `BlockerGroupSummary`. Add `workloadDistribution: WorkloadOwnerSummary[]` to `SupervisorDashboard`.
**What must be preserved:** All existing properties. The `getSupervisorDashboard` fetch function body is unchanged.

#### `src/ClaimManager.Frontend/src/features/dashboard/components/SupervisorDashboard.tsx`
**Current state:** `SupervisorDashboard` renders: risk signals grid, blocker summary section (chips navigating to claims queue), high-risk and aging claim sections (two-column grid), integration health panel. Stories 3.1 and 3.2 established `dashboardOrigin` state payload in all navigation calls.
**What this story changes:** Add `WorkloadDistributionSection` component. Enhance `BlockerSummarySection` to display systemic context from new DTO fields. Insert `WorkloadDistributionSection` between `BlockerSummarySection` and the two-column claims grid.
**What must be preserved:** All existing signal card, blocker chip, claim preview row behavior. The `dashboardOrigin` navigation pattern must be used consistently in new owner-row navigation. Do not alter the `IntegrationHealthPanel` position or the 30s stale / 60s refetch interval.

### UX Guidance

The UX spec defines the supervisor experience as intervention-focused, not passive reporting. The workload distribution view should help the supervisor act, not just observe:
- Show owner rows ranked by burden (highest stuck+aging first) so the most overloaded adjuster is immediately visible
- Color-code stuck and aging counts using the same semantic colors already used in signal cards (`error.main` for stuck, `warning.main` for aging)
- Keep each owner row compact and scannable — a dense table-like stack, not an expanded card per owner
- The row click navigates to the claims queue filtered by owner, with `dashboardOrigin` state, so the supervisor lands in the right filtered context to take action

For blocker patterns:
- The chip click already navigates to the filtered claims queue — preserve this
- The new systemic context ("3 adjusters") should appear without requiring the supervisor to expand or click the chip; use MUI `Tooltip` wrapping the `Chip` for hover detail, or show the adjuster count as a small secondary chip badge
- "Includes aging" warning below a blocker chip signals urgency without dominating the layout

Keep the section header language consistent with Story 3.1/3.2 patterns: overline (eyebrow) + h3 heading, as used in `ClaimPreviewSection`.

Desktop-first, high-density: The workload distribution list should use a compact `Stack` or table-like layout, not full cards. Supervisors reviewing this section are scanning for outliers, not reading prose.

### Previous Story Intelligence

**From Story 3.2 (drill-down):**
- The `dashboardOrigin` state shape is: `{ label: string; backTo: string }` — exactly `{ label: 'Workload: <ownerId>', backTo: '/' }` for owner rows.
- `RouterLink` and `useNavigate` are both already imported in `SupervisorDashboard.tsx`. Owner rows can use `useNavigate` in `WorkloadDistributionSection` with an `onClick` handler (same pattern as `BlockerSummarySection`).
- Story 3.2 modified `ClaimsQueuePage.tsx` to display the `dashboardOrigin` banner when state is present. The owner-row navigation URL must include a query param that `ClaimsQueuePage` already supports — verify that `ownedByUserId` is a supported filter param before assuming it works. If not supported, add it in scope of this story.
- 41 tests pass after Story 3.2; the new story must not break them. Run `npm test` before and after to confirm baseline.

**From Story 3.1 (dashboard foundation):**
- The supervisor seeded user is `supervisor@claimmanager.local` / `Supervisor!2345`.
- Admin user `admin@claimmanager.local` / `Admin!234567` also passes the supervisor policy.
- Adjuster access to `/api/supervisor-dashboard` returns 403 — this must remain true.
- `normalizedClaims` is already loaded via a single EF Core query; no additional DB calls are needed for this story's new computations.

**From Epic 2 retrospective:**
- Keep the backend as the source of truth for risk logic — do not reimplement workload heuristics in React components.
- Expose structured API fields instead of relying on frontend inference.
- When a persisted contract changes (here: DTO extension), update the story artifact, sprint tracker, and validation notes in the same working session.

### Library / Framework Requirements

Use the current repo stack exactly as established:
- Backend: .NET 10, ASP.NET Core minimal APIs, EF Core 10, Npgsql/PostgreSQL
- Frontend: React 19.2, React Router 7.9, TanStack Query 5, MUI 9, TypeScript 5.9, Vite 8
- API conventions: `camelCase` JSON, Problem Details for errors
- C# naming: `PascalCase` types and properties, `camelCase` local variables
- TypeScript naming: `camelCase` interface properties matching JSON serialization

MUI components already in `SupervisorDashboard.tsx` that the new section should reuse:
- `Paper`, `Stack`, `Typography`, `Box`, `Chip` — already imported
- `Tooltip` — not yet imported; add for blocker chip systemic context
- `useNavigate` — already imported (Story 3.2)
- `RouterLink` — already imported

No new npm packages. No new NuGet packages.

### Testing Requirements

**Backend functional tests** (`tests/ClaimManager.Api.FunctionalTests/SupervisorDashboardEndpointTests.cs`):
- Assert `workloadDistribution` key is present in the response JSON and is an array
- Assert `blockerSummary` entries include `affectedOwnerCount` and `agingClaimCount` (not null, not negative)
- If the test fixture seeds claims with different owners, assert ordering is descending by stuck+aging burden
- Confirm adjuster (non-supervisor) still receives 403

**Backend unit tests** (`tests/ClaimManager.Application.UnitTests`): Only add tests if the blocker pattern or workload logic is extracted to a testable helper method. If the computation stays inline in the endpoint (as it currently does), functional tests are sufficient.

**Frontend tests** (`tests/ClaimManager.Frontend.Tests/src/features/dashboard/supervisorDashboard.test.tsx`):
- Workload section renders when `workloadDistribution` has 2+ entries
- Workload section is hidden when `workloadDistribution` has 0 or 1 entries
- Owner row click navigates to `/claims?ownedByUserId=...` with `dashboardOrigin` state
- Blocker chip tooltip text includes "adjusters" when `affectedOwnerCount > 1`
- Blocker chip shows aging warning caption when `agingClaimCount > 0`

**Build validation** (must pass before story is done):
```
dotnet test tests/ClaimManager.Api.FunctionalTests
dotnet test tests/ClaimManager.Application.UnitTests
npm test                # from tests/ClaimManager.Frontend.Tests
npm run build           # from src/ClaimManager.Frontend
```

### Project Structure Notes

All files for this story fall within already-established module boundaries:
- `src/ClaimManager.Application/Dashboard/Dtos/` — DTO layer for dashboard capability (exists from Story 3.1)
- `src/ClaimManager.Api/Endpoints/Dashboard/` — API boundary for supervisor dashboard (exists from Story 3.1)
- `src/ClaimManager.Frontend/src/features/dashboard/` — frontend dashboard module (exists from Story 3.1, updated in 3.2)

No new directories, no new API endpoints, no new routes. This story is a vertical extension of the existing dashboard slice.

### References

- `d:/ws/bmad/_bmad-output/planning-artifacts/epics.md` — Epic 3, Story 3.3 acceptance criteria
- `d:/ws/bmad/_bmad-output/planning-artifacts/architecture.md` — `Application/Dashboard`, `Api/Endpoints/Dashboard`, `features/dashboard` module locations; TanStack Query state management rules; naming conventions
- `d:/ws/bmad/_bmad-output/planning-artifacts/ux-design-specification.md` — Supervisor flow: Detect Stuck Claim, Intervene Early; intervention workspace emphasis; desktop-first dense operations; semantic color usage
- `d:/ws/bmad/_bmad-output/implementation-artifacts/3-1-view-a-supervisor-risk-dashboard.md` — Dashboard architecture foundations, `normalizedClaims` query pattern, seeded supervisor user credentials
- `d:/ws/bmad/_bmad-output/implementation-artifacts/3-2-drill-from-operational-signals-into-claim-level-context.md` — `dashboardOrigin` state shape, navigation patterns, test file locations
- `d:/ws/bmad/_bmad-output/implementation-artifacts/epic-2-retro-2026-05-15.md` — Backend truth principle, structured API fields, regression coverage guidance
- `src/ClaimManager.Application/Dashboard/Dtos/SupervisorDashboardDto.cs` — current DTO definitions (extend in place)
- `src/ClaimManager.Api/Endpoints/Dashboard/DashboardEndpoints.cs` — current endpoint implementation (extend blockerSummary and add workloadDistribution)
- `src/ClaimManager.Frontend/src/features/dashboard/api/dashboardApi.ts` — current TypeScript interfaces (extend in place)
- `src/ClaimManager.Frontend/src/features/dashboard/components/SupervisorDashboard.tsx` — current dashboard component (add WorkloadDistributionSection, enrich BlockerSummarySection)
- `tests/ClaimManager.Api.FunctionalTests/SupervisorDashboardEndpointTests.cs` — existing functional tests (extend for new DTO fields)
- `tests/ClaimManager.Frontend.Tests/src/features/dashboard/supervisorDashboard.test.tsx` — existing frontend tests (extend for new component behavior)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Confirmed the story scope is a vertical extension of the supervisor dashboard slice, with no new routes or endpoints.
- Verified `ownedByUserId` is already supported by the claims query API, so workload rows can deep-link directly into the existing queue.
- Wrote red-phase contract tests for workload distribution and blocker pattern metadata before implementing the DTO and endpoint changes.
- Validated the new dashboard section and drill-down behavior with focused frontend tests and the dashboard API functional suite.

### Completion Notes List

- Added `WorkloadOwnerSummaryDto` and extended `BlockerGroupSummaryDto` / `SupervisorDashboardDto` to carry workload and systemic blocker context.
- Computed workload distribution and enriched blocker pattern counts directly from the dashboard endpoint's in-memory normalized claim set.
- Added a compact workload distribution section with owner-row drill-down to the claims queue and preserved dashboard origin state.
- Enriched blocker chips with adjuster-count and aging context while keeping the existing blocker drill-down navigation intact.
- Added/updated dashboard API functional coverage and frontend dashboard tests for workload visibility, queue navigation, and blocker tooltip context.
- Focused validation passed: `dotnet test tests/ClaimManager.Api.FunctionalTests --filter SupervisorDashboardEndpointTests`, `dotnet test tests/ClaimManager.Application.UnitTests`, `npm test -- src/features/dashboard/supervisorDashboard.test.tsx src/features/claims/claimsQueuePage.test.tsx`, `npm test -- src/features/dashboard/supervisorDashboard.test.tsx`, `npm test -- src/features/claims/claimForm.test.tsx`, `npm run build` in `src/ClaimManager.Frontend`.
- Full frontend suite still reports pre-existing failures in `src/features/claims/workflowActionsPanel.test.tsx` and suite-level timing failures under combined load; the story-specific dashboard slice passes in isolation.

### File List
- `_bmad-output/implementation-artifacts/3-3-detect-workload-imbalance-and-recurring-blocker-patterns.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `src/ClaimManager.Application/Dashboard/Dtos/SupervisorDashboardDto.cs`
- `src/ClaimManager.Api/Endpoints/Dashboard/DashboardEndpoints.cs`
- `src/ClaimManager.Frontend/src/features/dashboard/api/dashboardApi.ts`
- `src/ClaimManager.Frontend/src/features/dashboard/components/SupervisorDashboard.tsx`
- `src/ClaimManager.Frontend/src/features/claims/routes/ClaimsQueuePage.tsx`
- `tests/ClaimManager.Api.FunctionalTests/SupervisorDashboardEndpointTests.cs`
- `tests/ClaimManager.Frontend.Tests/src/features/dashboard/supervisorDashboard.test.tsx`

### Change Log

- Implemented dashboard workload distribution and enriched blocker pattern context for Story 3.3.
- Added regression coverage for backend contract fields, workload rows, blocker context, and queue drill-down behavior.
- Recorded validation results and noted the remaining unrelated full-suite failures.
