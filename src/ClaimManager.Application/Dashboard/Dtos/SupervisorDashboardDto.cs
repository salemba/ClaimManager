namespace ClaimManager.Application.Dashboard.Dtos;

public sealed record SupervisorDashboardSignals(
    int StuckCount,
    int AgingCount,
    int AttentionRequiredCount,
    int ApprovalPressureCount);

public sealed record BlockerGroupSummaryDto(
    string BlockerType,
    int Count);

public sealed record DashboardClaimPreviewDto(
    Guid Id,
    string ClaimNumber,
    string Status,
    string ClaimantName,
    string? BlockerType,
    string? OwnedByUserId,
    int DaysSinceCreated,
    bool HasDataIntegrityWarning);

public sealed record SupervisorDashboardDto(
    SupervisorDashboardSignals Signals,
    IReadOnlyList<BlockerGroupSummaryDto> BlockerSummary,
    IReadOnlyList<DashboardClaimPreviewDto> HighRiskClaims,
    IReadOnlyList<DashboardClaimPreviewDto> AgingClaims,
    DateTime GeneratedAtUtc);
