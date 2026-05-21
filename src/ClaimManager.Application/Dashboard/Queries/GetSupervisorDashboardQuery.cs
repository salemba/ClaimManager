namespace ClaimManager.Application.Dashboard.Queries;

using ClaimManager.Application.Dashboard.Dtos;
using ClaimManager.Domain.Claims;
using Microsoft.EntityFrameworkCore;

public sealed record GetSupervisorDashboardQuery;

public static class GetSupervisorDashboardHandler
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

    public static async Task<SupervisorDashboardDto> HandleAsync(
        IQueryable<Claim> claims,
        CancellationToken cancellationToken)
    {
        var generatedAt = DateTime.UtcNow;
        var agingCutoff = generatedAt.AddDays(-AgingThresholdDays);

        // Fetch active claims once to perform all computations in-memory.
        // This avoids multiple database round-trips while maintaining operational clarity.
        var allActiveClaims = await claims
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

        // 1. Compute high-level signals
        var signals = new SupervisorDashboardSignals(
            StuckCount: normalizedClaims.Count(c => c.BlockerType is not null),
            AgingCount: normalizedClaims.Count(c => c.CreatedAtUtc < agingCutoff),
            AttentionRequiredCount: normalizedClaims.Count(c => c.HasDataIntegrityWarning),
            ApprovalPressureCount: normalizedClaims.Count(c =>
                string.Equals(c.BlockerType, ApprovalBlockerType, StringComparison.OrdinalIgnoreCase)));

        // 2. Compute recurring blocker patterns (AC 2)
        var blockerSummary = normalizedClaims
            .Where(c => c.BlockerType is not null)
            .GroupBy(c => c.BlockerType!)
            .Select(g => new BlockerGroupSummaryDto(
                BlockerType: g.Key,
                Count: g.Count(),
                AffectedOwnerCount: g.Select(c => c.OwnedByUserId).Where(id => id is not null).Distinct().Count(),
                AgingClaimCount: g.Count(c => c.CreatedAtUtc < agingCutoff)))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.BlockerType)
            .ToArray();

        // 3. Compute workload distribution (AC 1)
        var workloadDistribution = normalizedClaims
            .GroupBy(c => c.OwnedByUserId ?? "unassigned")
            .Select(g => new WorkloadOwnerSummaryDto(
                OwnerId: g.Key,
                TotalCount: g.Count(),
                StuckCount: g.Count(c => c.BlockerType is not null),
                AgingCount: g.Count(c => c.CreatedAtUtc < agingCutoff),
                BlockerCount: g.Count(c => c.BlockerType is not null)))
            .OrderByDescending(o => o.StuckCount + o.AgingCount)
            .ThenByDescending(o => o.TotalCount)
            .ThenBy(o => o.OwnerId)
            .ToArray();

        // 4. Identify high-risk claims
        var highRiskClaims = normalizedClaims
            .Where(c => GetRiskScore(c) > 0)
            .OrderByDescending(GetRiskScore)
            .ThenBy(c => c.CreatedAtUtc)
            .Take(HighRiskPreviewLimit)
            .Select(c => MapToPreviewDto(c, generatedAt))
            .ToArray();

        // 5. Identify aging claims
        var agingClaims = normalizedClaims
            .Where(c => c.CreatedAtUtc < agingCutoff)
            .OrderBy(c => c.CreatedAtUtc)
            .Take(HighRiskPreviewLimit)
            .Select(c => MapToPreviewDto(c, generatedAt))
            .ToArray();

        return new SupervisorDashboardDto(
            signals,
            blockerSummary,
            highRiskClaims,
            agingClaims,
            workloadDistribution,
            generatedAt);
    }

    private static DashboardClaimPreviewDto MapToPreviewDto(ActiveClaim claim, DateTime generatedAt) =>
        new(
            claim.Id,
            claim.ClaimNumber,
            claim.Status,
            claim.ClaimantName,
            claim.BlockerType,
            claim.OwnedByUserId,
            (int)(generatedAt - claim.CreatedAtUtc).TotalDays,
            claim.HasDataIntegrityWarning);

    private static int GetRiskScore(ActiveClaim claim) =>
        claim.HasDataIntegrityWarning ? 2 : claim.BlockerType is not null ? 1 : 0;

    private static string? NormalizeBlockerType(string? blockerType) =>
        string.IsNullOrWhiteSpace(blockerType) ? null : blockerType.Trim();
}
