# Role
Act as a Senior .NET Backend Architect.

# Context
- User Story Requirements: #3-3-detect-workload-imbalance-and-recurring-blocker-patterns.md
- File to modify: #src/ClaimManager.Application/Dashboard/Dtos/SupervisorDashboardDto.cs

# Task
Based on Acceptance Criteria 1 and 2, extend the existing `SupervisorDashboardDto.cs` in place. 
Add the necessary structured types/records to support:
1. Workload distribution data across adjusters or teams (including claim load and aging pressure).
2. Recurring blocker patterns and types to highlight systemic workflow bottlenecks.

# Constraint
Do not touch any controllers or endpoints yet. Strictly follow the project's existing record/class conventions within this file. Ensure monetary or duration precisions are correctly typed.