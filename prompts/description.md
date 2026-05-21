# Refactoring: GetSupervisorDashboardQuery

## Overview
Moved the supervisor dashboard data aggregation logic from the API endpoint directly to an application-level query handler in `src/ClaimManager.Application/Dashboard/Queries/GetSupervisorDashboardQuery.cs`. This refactor enables better separation of concerns and follows the CQRS pattern.

## Changes

### 1. Application Layer
- **Interface Definition**: Created `IApplicationDbContext` in `src/ClaimManager.Application/Common/Interfaces/IApplicationDbContext.cs` to abstract the data layer and maintain architectural integrity (avoiding a direct dependency from Application to Infrastructure).
- **Query Handler**: Implemented `GetSupervisorDashboardQueryHandler` in `src/ClaimManager.Application/Dashboard/Queries/GetSupervisorDashboardQuery.cs`.
    - Integrated logic for computing workload distribution and recurring blocker patterns.
    - Used LINQ for efficient grouping and projection.
    - Populated all required fields in the updated `SupervisorDashboardDto`.
- **Project Configuration**: Added `Microsoft.EntityFrameworkCore` package reference to `ClaimManager.Application.csproj`.

### 2. Infrastructure Layer
- **DbContext Update**: Made `ClaimManagerDbContext` implement `IApplicationDbContext`.

### 3. API Layer
- **Endpoint Refactor**: Updated `DashboardEndpoints.cs` to delegate data retrieval to the new `GetSupervisorDashboardQueryHandler`.
- **DI Registration**: Registered `IApplicationDbContext` and `GetSupervisorDashboardQueryHandler` in `Program.cs`.

## Metrics Populated
- **Signals**: Stuck, Aging, Attention Required, and Approval Pressure counts.
- **Blocker Summary**: Detailed breakdown of blocker types with affected owner counts and aging claim context.
- **Workload Distribution**: Comprehensive view of adjuster load including total, stuck, aging, and blocker counts per owner.
- **Risk Preview**: High-risk and aging claims lists for immediate oversight.
