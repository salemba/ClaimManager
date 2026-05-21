# Role
Senior .NET Integration & Test Engineer.

# Context
- Endpoint File: #src/ClaimManager.Api/Endpoints/Dashboard/DashbordEndpoints.cs
- Query File: #src\ClaimManager.Application\Dashboard\Queries\GetSupervisorDashboardQuery.cs
- Functional Tests: #tests\ClaimManager.Api.FunctionalTests\SupervisorDashboardEndpointTests.cs

# Task
1. Review `DashbordEndpoints.cs` & `GetSupervisorDashboardQuery` to ensure the dashboard query maps correctly to the updated response structures without breaking the REST contract.
2. Extend `SupervisorDashboardEndpointTests.cs` to assert that the new workload metrics and blocker patterns are correctly returned by the API when a Supervisor user requests the dashboard.

# Constraint
1. Match the exact `Xunit`, `FluentAssertions`, and API Factory setups already established in the test file.
2. Run only on local envirment dont use docker

# Role
Senior .NET Integration & Test Engineer.

# Context
- Endpoint File: #src/ClaimManager.Api/Endpoints/Dashboard/DashbordEndpoints.cs
- Query File: #src\ClaimManager.Application\Dashboard\Queries\GetSupervisorDashboardQuery.cs
- Functional Tests: #tests\ClaimManager.Api.FunctionalTests\SupervisorDashboardEndpointTests.cs

# Task
1. Review `DashbordEndpoints.cs` & `GetSupervisorDashboardQuery` to ensure the dashboard query maps correctly to the updated response structures without breaking the REST contract.
2. Extend `SupervisorDashboardEndpointTests.cs` to assert that the new workload metrics and blocker patterns are correctly returned by the API when a Supervisor user requests the dashboard.

# Constraint
1. Match the exact `Xunit`, `FluentAssertions`, and API Factory setups already established in the test file.
2. Run only on local envirment dont use docker

# Stop & Ask IF
1. you intreduce new nuget library
2. find any ambiguity



