using ClaimManager.Application.Common.Interfaces;
using ClaimManager.Application.Dashboard.Dtos;
using ClaimManager.Domain.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClaimManager.Application.Dashboard.Queries;

public sealed record GetSupervisorDashboardQuery : IRequest<SupervisorDashboardDto>;

public sealed class GetSupervisorDashboardQueryHandler(IClaimManagerDbContext context, ISystemClock systemClock)
    : IRequestHandler<GetSupervisorDashboardQuery, SupervisorDashboardDto>
{
    public async Task<SupervisorDashboardDto> Handle(GetSupervisorDashboardQuery request, CancellationToken cancellationToken)
    {
        var utcNow = systemClock.UtcNow;
        var allClaims = await context.Claims.AsNoTracking().ToListAsync(cancellationToken);

        var signals = new SupervisorDashboardSignals(
            StuckCount: allClaims.Count(c => c.IsStuck(utcNow)),
            AgingCount: allClaims.Count(c => c.IsAging(utcNow)),
            AttentionRequiredCount: allClaims.Count(c => c.RequiresAttention()),
            ApprovalPressureCount: allClaims.Count(c => c.IsPendingApproval())
        );

        var blockerSummary = allClaims
            .Where(c => c.BlockerType != null)
            .GroupBy(c => c.BlockerType)
            .Select(g => new BlockerGroupSummaryDto(
                BlockerType: g.Key!,
                Count: g.Count(),
                AffectedOwnerCount: g.Select(c => c.OwnedByUserId).Distinct().Count(),
                AgingClaimCount: g.Count(c => c.IsAging(utcNow))
            ))
            .OrderByDescending(x => x.Count)
            .ToList();

        var highRiskClaims = allClaims
            .Where(c => c.RequiresAttention())
            .OrderByDescending(c => c.DaysSinceCreated(utcNow))
            .Take(10)
            .Select(c => new DashboardClaimPreviewDto(
                Id: c.Id,
                ClaimNumber: c.ClaimNumber,
                Status: c.Status,
                ClaimantName: c.ClaimantName,
                BlockerType: c.BlockerType,
                OwnedByUserId: c.OwnedByUserId,
                DaysSinceCreated: c.DaysSinceCreated(utcNow),
                HasDataIntegrityWarning: c.HasDataIntegrityWarning
             ))
            .ToList();

        var agingClaims = allClaims
            .Where(c => c.IsAging(utcNow))
            .OrderByDescending(c => c.DaysSinceCreated(utcNow))
            .Take(10)
            .Select(c => new DashboardClaimPreviewDto(
                Id: c.Id,
                ClaimNumber: c.ClaimNumber,
                Status: c.Status,
                ClaimantName: c.ClaimantName,
                BlockerType: c.BlockerType,
                OwnedByUserId: c.OwnedByUserId,
                DaysSinceCreated: c.DaysSinceCreated(utcNow),
                HasDataIntegrityWarning: c.HasDataIntegrityWarning
            ))
            .ToList();

        var workloadDistribution = allClaims
            .Where(c => !string.IsNullOrEmpty(c.OwnedByUserId))
            .GroupBy(c => c.OwnedByUserId)
            .Select(g => new WorkloadOwnerSummaryDto(
                OwnerId: g.Key!,
                TotalCount: g.Count(),
                StuckCount: g.Count(c => c.IsStuck(utcNow)),
                AgingCount: g.Count(c => c.IsAging(utcNow)),
                BlockerCount: g.Count(c => !string.IsNullOrEmpty(c.BlockerType))
            ))
            .OrderByDescending(x => x.TotalCount)
            .ToList();

        return new SupervisorDashboardDto(
            Signals: signals,
            BlockerSummary: blockerSummary,
            HighRiskClaims: highRiskClaims,
            AgingClaims: agingClaims,
            WorkloadDistribution: workloadDistribution,
            GeneratedAtUtc: utcNow.DateTime
        );
    }
}
