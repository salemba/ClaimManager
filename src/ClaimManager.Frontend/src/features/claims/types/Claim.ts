export interface ClaimsPage<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ClaimsQueryParams {
  search?: string;
  status?: string;
  blockerType?: string;
  hasBlocker?: boolean;
  ownedByUserId?: string;
  page?: number;
  pageSize?: number;
}

export interface ClaimAuditEntry {
  action: string;
  summary: string;
  performedAtUtc: string;
  performedByUserId: string;
}

export interface ClaimDataIntegrityIssue {
  dependency: string;
  message: string;
}

export interface ClaimReconciliation {
  attemptedAtUtc: string;
  retriedDependencies: string[];
  recoveredDependencies: string[];
  unresolvedDependencies: string[];
  summary: string;
  isFullyReconciled: boolean;
}

export interface ClaimNote {
  id: string;
  content: string;
  createdAtUtc: string;
  createdByUserId: string;
}

export interface ClaimCommunication {
  id: string;
  communicationType: string;
  channel: string;
  recipient: string;
  subject: string;
  status: string;
  attemptCount: number;
  lastAttemptAtUtc: string | null;
  deliveryId: string | null;
  failureReason: string | null;
  createdAtUtc: string;
  createdByUserId: string;
}

export interface ClaimDocument {
  id: string;
  fileName: string;
  fileType: string;
  contentType: string | null;
  fileSizeBytes: number;
  uploadedAtUtc: string;
  uploadedByUserId: string;
  source: string;
}

export interface ClaimSummary {
  id: string;
  claimNumber: string;
  status: string;
  claimantName: string;
  policyNumber: string;
  lossDateUtc: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  blockerType: string | null;
  blockerReason: string | null;
  blockedAtUtc: string | null;
  ownedByUserId: string | null;
  hasDataIntegrityWarning: boolean;
  policySyncedAtUtc: string | null;
  paymentSyncedAtUtc: string | null;
  documentSyncedAtUtc: string | null;
}

export interface Claim extends ClaimSummary {
  claimantEmail: string;
  claimantPhone: string;
  lossType: string;
  lossDescription: string;
  createdByUserId: string;
  updatedByUserId: string | null;
  nextExpectedAction: string | null;
  dataIntegrityWarningMessage: string | null;
  activeDataIntegrityIssues: ClaimDataIntegrityIssue[];
  reconciliation: ClaimReconciliation | null;
  policyHolder: string | null;
  coverageType: string | null;
  policyEffectiveDate: string | null;
  policyExpirationDate: string | null;
  paymentReference: string | null;
  paymentStatus: string | null;
  paymentAmount: number | null;
  paymentCurrency: string | null;
  paymentSettledAt: string | null;
  auditHistory: ClaimAuditEntry[];
  notes: ClaimNote[];
  documents: ClaimDocument[];
  communications: ClaimCommunication[];
  rowVersion: string;
  availableActions: string[];
}

export interface ClaimFormValues {
  claimantName: string;
  claimantEmail: string;
  claimantPhone: string;
  policyNumber: string;
  lossDateUtc: string;
  lossType: string;
  lossDescription: string;
}

export type ClaimFormErrors = Partial<Record<keyof ClaimFormValues, string>>;

export const emptyClaimFormValues: ClaimFormValues = {
  claimantName: '',
  claimantEmail: '',
  claimantPhone: '',
  policyNumber: '',
  lossDateUtc: '',
  lossType: '',
  lossDescription: '',
};

export function claimToFormValues(claim: Claim): ClaimFormValues {
  return {
    claimantName: claim.claimantName,
    claimantEmail: claim.claimantEmail,
    claimantPhone: claim.claimantPhone,
    policyNumber: claim.policyNumber,
    lossDateUtc: claim.lossDateUtc.slice(0, 10),
    lossType: claim.lossType,
    lossDescription: claim.lossDescription,
  };
}