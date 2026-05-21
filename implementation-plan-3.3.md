# Role
You are an expert Full-Stack .NET 10 and React 19 Developer, highly experienced with Clean Architecture, CQRS, EF Core, TanStack Query, and MUI.

# Task
Your objective is to implement "Story 3.3: Detect Workload Imbalance and Recurring Blocker Patterns". 

# Context
The complete technical specification, acceptance criteria, scope guardrails, and exact implementation details are documented in the following file:
`3-3-detect-workload-imbalance-and-recurring-blocker-patterns.md`

# Instructions

## 1. Preparation
- Read the file `3-3-detect-workload-imbalance-and-recurring-blocker-patterns.md` entirely. Pay special attention to the "Dev Notes", "Scope Guardrails", and "Architecture Compliance" sections.
- Do NOT introduce any new database queries. All data must be computed in-memory from the already fetched `normalizedClaims`.
- Do NOT introduce any new endpoints or routes. This is an extension of the existing supervisor dashboard.

## 2. Backend Implementation (C#)
- **DTOs:** Modify `src/ClaimManager.Application/Dashboard/Dtos/SupervisorDashboardDto.cs`. Add `WorkloadOwnerSummaryDto`. Extend `BlockerGroupSummaryDto` with `AffectedOwnerCount` and `AgingClaimCount`. Extend `SupervisorDashboardDto` with `WorkloadDistribution`. Preserve all existing positional record parameters.
- **API:** Modify `src/ClaimManager.Api/Endpoints/Dashboard/DashboardEndpoints.cs`. 
  - Update the `blockerSummary` LINQ projection to include `AffectedOwnerCount` (distinct owners) and `AgingClaimCount`.
  - Add the `workloadDistribution` LINQ projection grouped by `OwnedByUserId`.
  - Pass the new data to the `SupervisorDashboardDto` constructor.

## 3. Frontend Implementation (TypeScript / React)
- **API Contract:** Update `src/ClaimManager.Frontend/src/features/dashboard/api/dashboardApi.ts` to mirror the new backend DTOs (`WorkloadOwnerSummary`, extending `BlockerGroupSummary`, extending `SupervisorDashboard`).
- **UI Components:** Modify `src/ClaimManager.Frontend/src/features/dashboard/components/SupervisorDashboard.tsx`.
  - Create and inject a compact `WorkloadDistributionSection` between the Blocker Summary and the two-column claims grid. 
  - Ensure the owner rows navigate to `/claims?ownedByUserId=<ownerId>` while passing the strict `dashboardOrigin` state.
  - Enrich the existing `BlockerSummarySection` Chips using MUI `Tooltip` to show adjuster counts and add a secondary caption for aging claims using `warning.main` color.
  - Handle the visibility logic: only render the workload section if `workloadDistribution.length > 1`.

## 4. Testing
- **Backend Tests:** Update `tests/ClaimManager.Api.FunctionalTests/SupervisorDashboardEndpointTests.cs` to assert the presence and correctness of the new workload and blocker fields.
- **Frontend Tests:** Update `tests/ClaimManager.Frontend.Tests/src/features/dashboard/supervisorDashboard.test.tsx` to assert the rendering rules of the workload section, the correct `dashboardOrigin` navigation on click, and the new tooltip/caption in the blocker chips.

# Output Format
Execute the implementation step by step. Use the `replace_string_in_file` or `insert_edit_into_file` tools to apply the changes to the codebase. After completing the code, run the necessary dotnet and npm test commands in the terminal to validate the implementation.