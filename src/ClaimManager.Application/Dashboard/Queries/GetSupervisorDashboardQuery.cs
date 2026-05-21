using ClaimManager.Application.Dashboard.Dtos;
using ClaimManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace ClaimManager.Application.Dashboard.Queries;

public sealed record GetSupervisorDashboardQuery : IRequest<SupervisorDashboardDto>;

public class GetSupervisorDashboardQueryHandler(ClaimManagerDbContext context)
    : IRequestHandler<GetSupervisorDashboardQuery, SupervisorDashboardDto>
{
    public async Task<SupervisorDashboardDto> Handle(GetSupervisorDashboardQuery request, CancellationToken cancellationToken)
    {
        var claims = await context.Claims
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var signals = new SupervisorDashboardSignals(
            StuckCount: claims.Count(c => c.BlockerType != null),
            AgingCount: claims.Count(c => (DateTime.UtcNow - c.CreatedAtUtc).TotalDays > 30),
            AttentionRequiredCount: claims.Count(c => c.NextExpectedAction != null),
            ApprovalPressureCount: 0 // Placeholder
        );

        var blockerSummary = claims
            .Where(c => c.BlockerType != null)
            .GroupBy(c => c.BlockerType)
            .Select(g => new BlockerGroupSummaryDto(
                BlockerType: g.Key,
                Count: g.Count(),
                AffectedOwnerCount: g.Select(c => c.OwnedByUserId).Distinct().Count(),
                AgingClaimCount: g.Count(c => (DateTime.UtcNow - c.CreatedAtUtc).TotalDays > 30)
            ))
            .ToList();

        var workloadDistribution = claims
            .GroupBy(c => c.OwnedByUserId)
            .Select(g => new WorkloadOwnerSummaryDto(
                OwnerId: g.Key,
                TotalCount: g.Count(),
                StuckCount: g.Count(c => c.BlockerType != null),
                AgingCount: g.Count(c => (DateTime.UtcNow - c.CreatedAtUtc).TotalDays > 30),
                BlockerCount: g.Count(c => c.BlockerType != null)
            ))
            .ToList();

        var highRiskClaims = claims
            .Where(c => c.BlockerType != null) // Example high-risk condition
            .Select(c => new DashboardClaimPreviewDto(
                Id: c.Id,
                ClaimNumber: c.ClaimNumber,
                Status: c.Status,
                ClaimantName: c.ClaimantName,
                BlockerType: c.BlockerType,
                OwnedByUserId: c.OwnedByUserId,
                DaysSinceCreated: (int)(DateTime.UtcNow - c.CreatedAtUtc).TotalDays,
                HasDataIntegrityWarning: false
            ))
            .Take(10)
            .ToList();

        var agingClaims = claims
            .Where(c => (DateTime.UtcNow - c.CreatedAtUtc).TotalDays > 30)
            .Select(c => new DashboardClaimPreviewDto(
                Id: c.Id,
                ClaimNumber: c.ClaimNumber,
                Status: c.Status,
                ClaimantName: c.ClaimantName,
                BlockerType: c.BlockerType,
                OwnedByUserId: c.OwnedByUserId,
                DaysSinceCreated: (int)(DateTime.UtcNow - c.CreatedAtUtc).TotalDays,
                HasDataIntegrityWarning: false
            ))
            .Take(10)
            .ToList();

        return new SupervisorDashboardDto(
            Signals: signals,
            BlockerSummary: blockerSummary,
            HighRiskClaims: highRiskClaims,
            AgingClaims: agingClaims,
            WorkloadDistribution: workloadDistribution,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }
}
