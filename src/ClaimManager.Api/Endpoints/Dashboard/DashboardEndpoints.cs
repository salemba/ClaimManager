namespace ClaimManager.Api.Endpoints.Dashboard;

using ClaimManager.Application.Dashboard.Dtos;
using ClaimManager.Application.Security;
using ClaimManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

public static class DashboardEndpoints
{
    private static readonly HashSet<string> _terminalStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "closed", "resolved", "complete" };

    private const int AgingThresholdDays = 14;
    private const int HighRiskPreviewLimit = 10;
    private const string ApprovalBlockerType = "awaiting-payment-approval";

    private sealed record ActiveClaim(
        Guid Id,
        string ClaimNumber,
        string Status,
        string ClaimantName,
        string? BlockerType,
        string? OwnedByUserId,
        DateTime CreatedAtUtc,
        bool HasDataIntegrityWarning);

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
        var generatedAt = DateTime.UtcNow;
        var agingCutoff = generatedAt.AddDays(-AgingThresholdDays);

        var allActiveClaims = await dbContext.Claims
            .Where(c => !_terminalStatuses.Contains(c.Status))
            .Select(c => new
            {
                c.Id,
                c.ClaimNumber,
                c.Status,
                c.ClaimantName,
                c.BlockerType,
                c.OwnedByUserId,
                c.CreatedAtUtc,
                c.HasDataIntegrityWarning,
            })
            .ToArrayAsync(cancellationToken);

        var normalizedClaims = allActiveClaims
            .Select(c => new ActiveClaim(
                c.Id,
                c.ClaimNumber,
                c.Status,
                c.ClaimantName,
                NormalizeBlockerType(c.BlockerType),
                c.OwnedByUserId,
                c.CreatedAtUtc,
                c.HasDataIntegrityWarning))
            .ToArray();

        var stuckCount = normalizedClaims.Count(c => c.BlockerType is not null);
        var agingCount = normalizedClaims.Count(c => c.CreatedAtUtc < agingCutoff);
        var attentionRequiredCount = normalizedClaims.Count(c => c.HasDataIntegrityWarning);
        var approvalPressureCount = normalizedClaims.Count(c =>
            string.Equals(c.BlockerType, ApprovalBlockerType, StringComparison.OrdinalIgnoreCase));

        var signals = new SupervisorDashboardSignals(
            stuckCount,
            agingCount,
            attentionRequiredCount,
            approvalPressureCount);

        var blockerSummary = normalizedClaims
            .Where(c => c.BlockerType is not null)
            .GroupBy(c => c.BlockerType!)
            .Select(g => new BlockerGroupSummaryDto(
                g.Key,
                g.Count(),
                g.Select(c => c.OwnedByUserId).Where(ownerId => ownerId is not null).Distinct().Count(),
                g.Count(c => c.CreatedAtUtc < agingCutoff)))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.BlockerType)
            .ToArray();

        var workloadDistribution = normalizedClaims
            .GroupBy(c => c.OwnedByUserId ?? "unassigned")
            .Select(g => new WorkloadOwnerSummaryDto(
                g.Key,
                g.Count(),
                g.Count(c => c.BlockerType is not null),
                g.Count(c => c.CreatedAtUtc < agingCutoff),
                g.Select(c => c.BlockerType).Where(bt => bt is not null).Distinct().Count()))
            .OrderByDescending(o => o.StuckCount + o.AgingCount)
            .ThenByDescending(o => o.TotalCount)
            .ThenBy(o => o.OwnerId)
            .ToArray();

        var highRiskClaims = normalizedClaims
            .Where(c => GetRiskScore(c) > 0)
            .OrderByDescending(GetRiskScore)
            .ThenBy(c => c.CreatedAtUtc)
            .Take(HighRiskPreviewLimit)
            .Select(c => new DashboardClaimPreviewDto(
                c.Id,
                c.ClaimNumber,
                c.Status,
                c.ClaimantName,
                c.BlockerType,
                c.OwnedByUserId,
                (int)(generatedAt - c.CreatedAtUtc).TotalDays,
                c.HasDataIntegrityWarning))
            .ToArray();

        var agingClaims = normalizedClaims
            .Where(c => c.CreatedAtUtc < agingCutoff)
            .OrderBy(c => c.CreatedAtUtc)
            .Take(HighRiskPreviewLimit)
            .Select(c => new DashboardClaimPreviewDto(
                c.Id,
                c.ClaimNumber,
                c.Status,
                c.ClaimantName,
                c.BlockerType,
                c.OwnedByUserId,
                (int)(generatedAt - c.CreatedAtUtc).TotalDays,
                c.HasDataIntegrityWarning))
            .ToArray();

        return TypedResults.Ok(new SupervisorDashboardDto(signals, blockerSummary, highRiskClaims, agingClaims, workloadDistribution, generatedAt));
    }

    private static int GetRiskScore(ActiveClaim claim) =>
        claim.HasDataIntegrityWarning ? 2 : claim.BlockerType is not null ? 1 : 0;

    private static string? NormalizeBlockerType(string? blockerType) =>
        string.IsNullOrWhiteSpace(blockerType) ? null : blockerType.Trim();
}
