namespace ClaimManager.Application.Dashboard.Queries;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClaimManager.Application.Dashboard.Dtos;
using ClaimManager.Domain.Claims;
using Microsoft.EntityFrameworkCore;

public interface IClaimManagerDbContext
{
    DbSet<Claim> Claims { get; }
}

public sealed record GetSupervisorDashboardQuery;

public sealed class GetSupervisorDashboardQueryHandler
{
    private readonly IClaimManagerDbContext _dbContext;

    public GetSupervisorDashboardQueryHandler(IClaimManagerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SupervisorDashboardDto> Handle(GetSupervisorDashboardQuery request, CancellationToken cancellationToken)
    {
        var generatedAt = DateTime.UtcNow;
        var agingCutoff = generatedAt.AddDays(-14);
        var terminalStatuses = new[] { "closed", "resolved", "complete" };
        var approvalBlockerType = "awaiting-payment-approval";

        // 1. Calculate global signals
        var signalsDbResult = await _dbContext.Claims
            .Where(c => !terminalStatuses.Contains(c.Status))
            .GroupBy(c => 1)
            .Select(g => new
            {
                StuckCount = g.Count(c => c.BlockerType != null),
                AgingCount = g.Count(c => c.CreatedAtUtc < agingCutoff),
                AttentionRequiredCount = g.Count(c => c.HasDataIntegrityWarning),
                ApprovalPressureCount = g.Count(c => c.BlockerType == approvalBlockerType)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var signals = new SupervisorDashboardSignals(
            signalsDbResult?.StuckCount ?? 0,
            signalsDbResult?.AgingCount ?? 0,
            signalsDbResult?.AttentionRequiredCount ?? 0,
            signalsDbResult?.ApprovalPressureCount ?? 0);

        // 2. Identify recurring blocker patterns (AC 2)
        var blockerDbResult = await _dbContext.Claims
            .Where(c => !terminalStatuses.Contains(c.Status) && c.BlockerType != null)
            .GroupBy(c => c.BlockerType!)
            .Select(g => new
            {
                BlockerType = g.Key,
                Count = g.Count(),
                AffectedOwnerCount = g.Select(c => c.OwnedByUserId).Distinct().Count(id => id != null),
                AgingClaimCount = g.Count(c => c.CreatedAtUtc < agingCutoff)
            })
            .ToArrayAsync(cancellationToken);

        var blockerSummary = blockerDbResult
            .Select(b => new BlockerGroupSummaryDto(
                b.BlockerType.Trim(),
                b.Count,
                b.AffectedOwnerCount,
                b.AgingClaimCount))
            .OrderByDescending(b => b.Count)
            .ThenBy(b => b.BlockerType)
            .ToArray();

        // 3. Compute adjuster load / Workload distribution (AC 1)
        var workloadDbResult = await _dbContext.Claims
            .Where(c => !terminalStatuses.Contains(c.Status))
            .GroupBy(c => c.OwnedByUserId ?? "unassigned")
            .Select(g => new
            {
                OwnerId = g.Key,
                TotalCount = g.Count(),
                StuckCount = g.Count(c => c.BlockerType != null),
                AgingCount = g.Count(c => c.CreatedAtUtc < agingCutoff),
                BlockerCount = g.Count(c => c.BlockerType != null)
            })
            .ToArrayAsync(cancellationToken);

        var workloadDistribution = workloadDbResult
            .Select(w => new WorkloadOwnerSummaryDto(
                w.OwnerId,
                w.TotalCount,
                w.StuckCount,
                w.AgingCount,
                w.BlockerCount))
            .OrderByDescending(w => w.StuckCount + w.AgingCount)
            .ThenByDescending(w => w.TotalCount)
            .ThenBy(w => w.OwnerId)
            .ToArray();

        // 4. Fetch Top 10 High Risk Claims
        var highRiskClaims = await _dbContext.Claims
            .Where(c => !terminalStatuses.Contains(c.Status) && (c.HasDataIntegrityWarning || c.BlockerType != null))
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
                RiskScore = c.HasDataIntegrityWarning ? 2 : 1
            })
            .OrderByDescending(c => c.RiskScore)
            .ThenBy(c => c.CreatedAtUtc)
            .Take(10)
            .ToArrayAsync(cancellationToken);

        var highRiskDtos = highRiskClaims
            .Select(c => new DashboardClaimPreviewDto(
                c.Id,
                c.ClaimNumber,
                c.Status,
                c.ClaimantName,
                c.BlockerType?.Trim(),
                c.OwnedByUserId,
                (int)(generatedAt - c.CreatedAtUtc).TotalDays,
                c.HasDataIntegrityWarning))
            .ToArray();

        // 5. Fetch Top 10 Aging Claims
        var agingClaims = await _dbContext.Claims
            .Where(c => !terminalStatuses.Contains(c.Status) && c.CreatedAtUtc < agingCutoff)
            .OrderBy(c => c.CreatedAtUtc)
            .Take(10)
            .Select(c => new
            {
                c.Id,
                c.ClaimNumber,
                c.Status,
                c.ClaimantName,
                c.BlockerType,
                c.OwnedByUserId,
                c.CreatedAtUtc,
                c.HasDataIntegrityWarning
            })
            .ToArrayAsync(cancellationToken);

        var agingDtos = agingClaims
            .Select(c => new DashboardClaimPreviewDto(
                c.Id,
                c.ClaimNumber,
                c.Status,
                c.ClaimantName,
                c.BlockerType?.Trim(),
                c.OwnedByUserId,
                (int)(generatedAt - c.CreatedAtUtc).TotalDays,
                c.HasDataIntegrityWarning))
            .ToArray();

        return new SupervisorDashboardDto(
            signals,
            blockerSummary,
            highRiskDtos,
            agingDtos,
            workloadDistribution,
            generatedAt);
    }
}
