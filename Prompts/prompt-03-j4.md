prompt-03-j4.md
# Role
Senior .NET Integration & Test Engineer.

# Context
- Endpoint File: #src/ClaimManager.Api/Endpoints/Dashboard/DashboardEndpoints.cs
- Functional Tests: #tests/ClaimManager.Api.FunctionalTests/SupervisorDashboardEndpointTests.cs

# Task
1. Review `DashboardEndpoints.cs` to ensure the dashboard query maps correctly to the updated response structures without breaking the REST contract.
2. Extend `SupervisorDashboardEndpointTests.cs` to assert that the new workload metrics and blocker patterns are correctly returned by the API when a Supervisor user requests the dashboard.

# Constraint
Match the exact `Xunit`, `FluentAssertions`, and API Factory setups already established in the test file.