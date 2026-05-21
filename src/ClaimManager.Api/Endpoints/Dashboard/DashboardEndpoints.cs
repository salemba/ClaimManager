namespace ClaimManager.Api.Endpoints.Dashboard;

using ClaimManager.Application.Dashboard.Dtos;
using ClaimManager.Application.Dashboard.Queries;
using ClaimManager.Application.Security;
using ClaimManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/api/supervisor-dashboard", GetSupervisorDashboardAsync)
            .WithTags("Dashboard")
            .RequireAuthorization(ClaimManagerPolicies.Supervisor);

        return endpoints;
    }

    private static async Task<Ok<SupervisorDashboardDto>> GetSupervisorDashboardAsync(
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var result = await GetSupervisorDashboardHandler.HandleAsync(dbContext.Claims, cancellationToken);
        return TypedResults.Ok(result);
    }
}
