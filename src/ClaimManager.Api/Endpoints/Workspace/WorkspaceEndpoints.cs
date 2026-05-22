namespace ClaimManager.Api.Endpoints.Workspace;

using ClaimManager.Api.Configuration;
using ClaimManager.Api.Endpoints.Auth;
using ClaimManager.Application.Security;
using ClaimManager.Domain.Claims;
using ClaimManager.Infrastructure.Identity;
using ClaimManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Security.Claims;

public static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api").WithTags("Workspace");

        group.MapGet("/workspace", GetWorkspaceAsync)
            .RequireAuthorization(ClaimManagerPolicies.Adjuster);

        group.MapGet("/workspace/integration-health", GetIntegrationHealthAsync)
            .RequireAuthorization(ClaimManagerPolicies.Adjuster, ClaimManagerPolicies.Supervisor);

        group.MapGet("/admin/audit", GetAdminAuditAsync)
            .RequireAuthorization(ClaimManagerPolicies.Admin);

        return endpoints;
    }

    private static async Task<Ok<WorkspaceResponse>> GetWorkspaceAsync(
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal)
            ?? throw new InvalidOperationException("Authenticated user is required for the workspace endpoint.");

        var roles = await userManager.GetRolesAsync(user);
        var claims = await dbContext.Claims
            .OrderBy(claim => claim.ClaimNumber)
            .Select(claim => new ClaimSummary(claim.ClaimNumber, claim.Status, claim.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        var databaseAvailable = await dbContext.Database.CanConnectAsync(cancellationToken);
        AntiforgeryTokenCookie.Refresh(httpContext, antiforgery);

        return TypedResults.Ok(new WorkspaceResponse(
            User: new AuthEndpoints.AuthSessionResponse(user.Id.ToString(), user.Email ?? user.UserName ?? string.Empty, roles.ToArray()),
            DatabaseAvailable: databaseAvailable,
            Claims: claims));
    }

    private static async Task<Ok<IntegrationHealthResponse>> GetIntegrationHealthAsync(
        HealthCheckService healthCheckService,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(
            r => r.Tags.Contains("integration"),
            cancellationToken);
        var reportedAt = DateTime.UtcNow;

        var entries = await BuildIntegrationHealthEntriesAsync(report, reportedAt, dbContext, cancellationToken);

        return TypedResults.Ok(new IntegrationHealthResponse(entries, reportedAt));
    }

    private static async Task<IntegrationHealthEntry[]> BuildIntegrationHealthEntriesAsync(
        HealthReport report,
        DateTime reportedAt,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var boundaryNames = report.Entries.Keys.OrderBy(name => name).ToArray();
        if (boundaryNames.Length == 0)
        {
            return [];
        }

        var incidents = await dbContext.IntegrationHealthIncidents
            .Where(incident => boundaryNames.Contains(incident.BoundaryName))
            .OrderByDescending(incident => incident.StartedAtUtc)
            .ToListAsync(cancellationToken);

        var openIncidentsByBoundary = incidents
            .Where(incident => incident.ResolvedAtUtc is null)
            .GroupBy(incident => incident.BoundaryName)
            .ToDictionary(group => group.Key, group => group.First());

        var latestResolvedByBoundary = incidents
            .Where(incident => incident.ResolvedAtUtc is not null)
            .GroupBy(incident => incident.BoundaryName)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(incident => incident.ResolvedAtUtc).First());

        var hasChanges = false;

        foreach (var (boundaryName, entry) in report.Entries.OrderBy(kvp => kvp.Key))
        {
            var normalizedStatus = NormalizeHealthStatus(entry.Status);
            var description = entry.Description ?? string.Empty;

            if (entry.Status == HealthStatus.Healthy)
            {
                if (openIncidentsByBoundary.Remove(boundaryName, out var openIncident))
                {
                    openIncident.ResolvedAtUtc = reportedAt;
                    latestResolvedByBoundary[boundaryName] = openIncident;
                    hasChanges = true;
                }

                continue;
            }

            if (!openIncidentsByBoundary.TryGetValue(boundaryName, out var activeIncident))
            {
                activeIncident = new ClaimManager.Domain.Audit.IntegrationHealthIncident
                {
                    Id = Guid.NewGuid(),
                    BoundaryName = boundaryName,
                    Status = normalizedStatus,
                    Description = description,
                    StartedAtUtc = reportedAt,
                };

                dbContext.IntegrationHealthIncidents.Add(activeIncident);
                openIncidentsByBoundary[boundaryName] = activeIncident;
                hasChanges = true;
                continue;
            }

            if (!string.Equals(activeIncident.Status, normalizedStatus, StringComparison.Ordinal) ||
                !string.Equals(activeIncident.Description, description, StringComparison.Ordinal))
            {
                activeIncident.Status = normalizedStatus;
                activeIncident.Description = description;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return report.Entries
            .OrderBy(kvp => kvp.Key)
            .Select(kvp =>
            {
                openIncidentsByBoundary.TryGetValue(kvp.Key, out var activeIncident);
                latestResolvedByBoundary.TryGetValue(kvp.Key, out var latestResolvedIncident);

                return new IntegrationHealthEntry(
                    kvp.Key,
                    NormalizeHealthStatus(kvp.Value.Status),
                    kvp.Value.Description ?? string.Empty,
                    activeIncident?.StartedAtUtc,
                    latestResolvedIncident?.ResolvedAtUtc);
            })
            .ToArray();
    }

    private static string NormalizeHealthStatus(HealthStatus status) =>
        status.ToString().ToLowerInvariant();

    private static async Task<Ok<AdminAuditResponse>> GetAdminAuditAsync(
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal)
            ?? throw new InvalidOperationException("Authenticated user is required for the admin endpoint.");

        var roles = await userManager.GetRolesAsync(user);

        return TypedResults.Ok(new AdminAuditResponse(user.Email ?? user.UserName ?? string.Empty, roles.ToArray()));
    }

    public sealed record WorkspaceResponse(AuthEndpoints.AuthSessionResponse User, bool DatabaseAvailable, ClaimSummary[] Claims);

    public sealed record ClaimSummary(string ClaimNumber, string Status, DateTime CreatedAtUtc);

    public sealed record IntegrationHealthEntry(
        string Name,
        string Status,
        string Description,
        DateTime? ActiveIncidentStartedAtUtc,
        DateTime? LastResolvedIncidentAtUtc);

    public sealed record IntegrationHealthResponse(IntegrationHealthEntry[] Entries, DateTime ReportedAtUtc);

    public sealed record AdminAuditResponse(string Email, string[] Roles);
}