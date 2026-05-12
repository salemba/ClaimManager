namespace ClaimManager.Api.Endpoints.Workspace;

using ClaimManager.Api.Endpoints.Auth;
using ClaimManager.Application.Security;
using ClaimManager.Domain.Claims;
using ClaimManager.Infrastructure.Identity;
using ClaimManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api").WithTags("Workspace");

        group.MapGet("/workspace", GetWorkspaceAsync)
            .RequireAuthorization(ClaimManagerPolicies.Adjuster);

        group.MapGet("/admin/audit", GetAdminAuditAsync)
            .RequireAuthorization(ClaimManagerPolicies.Admin);

        return endpoints;
    }

    private static async Task<Ok<WorkspaceResponse>> GetWorkspaceAsync(
        ClaimsPrincipal principal,
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

        return TypedResults.Ok(new WorkspaceResponse(
            User: new AuthEndpoints.AuthSessionResponse(user.Id.ToString(), user.Email ?? user.UserName ?? string.Empty, roles.ToArray()),
            DatabaseAvailable: databaseAvailable,
            Claims: claims));
    }

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

    public sealed record AdminAuditResponse(string Email, string[] Roles);
}