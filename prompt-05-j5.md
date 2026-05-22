# Role
Senior DDD & .NET Developer.

# Context
- User Story Requirements: #docs/3-3-detect-workload-imbalance-and-recurring-blocker-patterns.md

# Task
1. introduce a new state Suspended witch is used by the supervisor to manually intervene and bypass the new workflow.
2. apply changes to introduce a "break-glass" capability for supervisors. that allows them to bypass standard workflow constraints for claims that are either stalled (older than 48 hours) or of high financial significance (amount > 10,000). The specific actions allowed are changing the assigned adjuster (`adjusterId`) and forcing a transition to a new workflow state and use the suspended status.
3. add all the rquired unit tests
4. check solution builds & runs without any issues or regressions.

# Constraint
1. Match the exact `Xunit`, `FluentAssertions`, and API Factory setups already established in the test file.
2. To Run use 
```bash
cd ClaimManager.AppHost
dotnet run
```

# Stop & Ask IF
1. you intreduce new nuget library
2. find any ambiguity