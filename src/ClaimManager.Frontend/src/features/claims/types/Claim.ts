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

export interface ClaimNote {
  id: string;
  content: string;
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
  ownedByUserId: string | null;
  hasDataIntegrityWarning: boolean;
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
  auditHistory: ClaimAuditEntry[];
  notes: ClaimNote[];
  documents: ClaimDocument[];
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