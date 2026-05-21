using ClaimManager.Application.Common.Interfaces;
using ClaimManager.Application.Dashboard.Dtos;
using ClaimManager.Domain.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimManager.Application.Dashboard.Queries;

public sealed record GetSupervisorDashboardQuery;

public class GetSupervisorDashboardQueryHandler : IRequestHandler<GetSupervisorDashboardQuery, SupervisorDashboardDto>
{
    private readonly IClaimManagerDbContext _context;
    private readonly ICurrentUser _user;
    private readonly IDateTime _dateTime;

    public GetSupervisorDashboardQueryHandler(IClaimManagerDbContext context, ICurrentUser user, IDateTime dateTime)
    {
        _context = context;
        _user = user;
        _dateTime = dateTime;
    }

    public async Task<SupervisorDashboardDto> Handle(GetSupervisorDashboardQuery request, CancellationToken cancellationToken)
    {
        var now = _dateTime.UtcNow;
        var agingThreshold = now.AddDays(-30);
        var highRiskThreshold = now.AddDays(-10);

        var claims = await _context.Claims
            .AsNoTracking()
            .Where(c => c.Status != "Closed" && c.Status != "Settled")
            .ToListAsync(cancellationToken);

        var signals = new SupervisorDashboardSignals(
            StuckCount: claims.Count(c => c.BlockerType != null),
            AgingCount: claims.Count(c => c.CreatedAtUtc < agingThreshold),
            AttentionRequiredCount: claims.Count(c => c.HasDataIntegrityWarning),
            ApprovalPressureCount: 0 // Placeholder
        );

        var blockerSummary = claims
            .Where(c => c.BlockerType != null)
            .GroupBy(c => c.BlockerType)
            .Select(g => new BlockerGroupSummaryDto(
                BlockerType: g.Key,
                Count: g.Count(),
                AffectedOwnerCount: g.Select(c => c.OwnedByUserId).Distinct().Count(),
                AgingClaimCount: g.Count(c => c.CreatedAtUtc < agingThreshold)
            ))
            .OrderByDescending(x => x.Count)
            .ToList();

        var workloadDistribution = claims
            .GroupBy(c => c.OwnedByUserId)
            .Select(g => new WorkloadOwnerSummaryDto(
                OwnerId: g.Key ?? "Unassigned",
                TotalCount: g.Count(),
                StuckCount: g.Count(c => c.BlockerType != null),
                AgingCount: g.Count(c => c.CreatedAtUtc < agingThreshold),
                BlockerCount: g.Count(c => c.BlockerType != null)
            ))
            .OrderByDescending(x => x.TotalCount)
            .ToList();

        var highRiskClaims = claims
            .Where(c => c.CreatedAtUtc < highRiskThreshold && c.BlockerType != null)
            .OrderBy(c => c.CreatedAtUtc)
            .Take(10)
            .Select(c => new DashboardClaimPreviewDto(
                c.Id,
                c.ClaimNumber,
                c.Status,
                c.ClaimantName,
                c.BlockerType,
                c.OwnedByUserId,
                (now - c.CreatedAtUtc).Days,
                c.HasDataIntegrityWarning
            ))
            .ToList();

        var agingClaims = claims
            .Where(c => c.CreatedAtUtc < agingThreshold)
            .OrderBy(c => c.CreatedAtUtc)
            .Take(10)
            .Select(c => new DashboardClaimPreviewDto(
                c.Id,
                c.ClaimNumber,
                c.Status,
                c.ClaimantName,
                c.BlockerType,
                c.OwnedByUserId,
                (now - c.CreatedAtUtc).Days,
                c.HasDataIntegrityWarning
            ))
            .ToList();

        return new SupervisorDashboardDto(
            Signals: signals,
            BlockerSummary: blockerSummary,
            HighRiskClaims: highRiskClaims,
            AgingClaims: agingClaims,
            WorkloadDistribution: workloadDistribution,
            GeneratedAtUtc: now
        );
    }
}
