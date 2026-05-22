export enum ClaimStatus {
  RECEIVED = "RECEIVED",
  UNDER_REVIEW = "UNDER_REVIEW",
  PENDING_DOCS = "PENDING_DOCS",
  EXPERT_ASSIGNED = "EXPERT_ASSIGNED",
  VALIDATED = "VALIDATED",
  CLOSED = "CLOSED",
}

export interface Claim {
  readonly id: string;
  readonly adjusterId: string;
  readonly status: ClaimStatus;
  readonly amount: number; // in euros
  readonly blockedSince: Date | null;
  readonly createdAt: Date;
}
