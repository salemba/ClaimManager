import { Claim, ClaimStatus } from '../domain/claim';
import { ClaimIsClosedError } from '../domain/errors';

const workflowOrder: readonly ClaimStatus[] = [
  ClaimStatus.RECEIVED,
  ClaimStatus.UNDER_REVIEW,
  ClaimStatus.PENDING_DOCS,
  ClaimStatus.EXPERT_ASSIGNED,
  ClaimStatus.VALIDATED,
  ClaimStatus.CLOSED,
];

export class WorkflowService {
  public getNextState(currentStatus: ClaimStatus): ClaimStatus {
    const currentIndex = workflowOrder.indexOf(currentStatus);
    if (currentIndex === -1) {
      // This should ideally not happen with valid data
      throw new Error(`Unknown claim status: ${currentStatus}`);
    }

    if (currentStatus === ClaimStatus.CLOSED) {
      throw new ClaimIsClosedError("Cannot determine next state for a closed claim.");
    }

    // Since we checked for CLOSED, the next index is always valid.
    return workflowOrder[currentIndex + 1];
  }
}
