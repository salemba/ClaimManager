import { apiFetch } from '../../../shared/api/client';

export interface SupervisorDashboardSignals {
  stuckCount: number;
  agingCount: number;
  attentionRequiredCount: number;
  approvalPressureCount: number;
}

export interface BlockerGroupSummary {
  blockerType: string;
  count: number;
  affectedOwnerCount: number;
  agingClaimCount: number;
}

export interface WorkloadOwnerSummary {
  ownerId: string;
  totalCount: number;
  stuckCount: number;
  agingCount: number;
  blockerCount: number;
}

export interface DashboardClaimPreview {
  id: string;
  claimNumber: string;
  status: string;
  claimantName: string;
  blockerType: string | null;
  ownedByUserId: string | null;
  daysSinceCreated: number;
  hasDataIntegrityWarning: boolean;
}

export interface SupervisorDashboard {
  signals: SupervisorDashboardSignals;
  blockerSummary: BlockerGroupSummary[];
  highRiskClaims: DashboardClaimPreview[];
  agingClaims: DashboardClaimPreview[];
  workloadDistribution: WorkloadOwnerSummary[];
  generatedAtUtc: string;
}

export async function getSupervisorDashboard() {
  return apiFetch<SupervisorDashboard>('/api/supervisor-dashboard');
}
