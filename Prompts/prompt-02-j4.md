# Role
Senior DDD & .NET Developer.

# Context
- Target Query: #src/ClaimManager.Application/Dashboard/Queries/GetSupervisorDashboardQuery.cs
- Updated DTO Contract: #src/ClaimManager.Application/Dashboard/Dtos/SupervisorDashboardDto.cs
- Database Truth: #src/ClaimManager.Infrastructure/Persistence/ClaimManagerDbContext.cs

# Task
Refactor the handler in `GetSupervisorDashboardQuery.cs` to populate the new workload distribution and recurring blocker pattern fields we just added to the DTO.

# Rules
- Do not execute raw SQL queries. Use LINQ queries against the `ClaimManagerDbContext`.
- Group claims efficiently to compute adjuster load, aging pressure metrics, and identify recurring blocker patterns (AC 1 & AC 2).
- Respect async/await standards and ensure cancellation tokens are passed correctly to EF Core operations.