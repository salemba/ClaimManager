namespace ClaimManager.Api.Endpoints.Dashboard;

using ClaimManager.Application.Dashboard.Dtos;
using ClaimManager.Application.Dashboard.Queries;
using ClaimManager.Application.Security;
using ClaimManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/api/supervisor-dashboard", (GetSupervisorDashboardQueryHandler handler, CancellationToken ct) => handler.HandleAsync(new GetSupervisorDashboardQuery(), ct))
            .WithTags("Dashboard")
            .RequireAuthorization(ClaimManagerPolicies.Supervisor);

        return endpoints;
    }
}
