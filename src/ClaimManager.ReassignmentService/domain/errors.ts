export class BusinessError extends Error {
  constructor(message: string) {
    super(message);
    this.name = this.constructor.name;
  }
}

export class ReassignmentConditionsNotMetError extends BusinessError {
  constructor(reasons: string[]) {
    super(`Reassignment conditions not met. Reasons: ${reasons.join(', ')}`);
  }
}

export class ClaimIsClosedError extends BusinessError {
  constructor(claimId: string) {
    super(`Claim with id ${claimId} is closed and cannot be reassigned.`);
  }
}

export class ClaimNotFoundError extends BusinessError {
    constructor(claimId: string) {
        super(`Claim with id ${claimId} not found.`);
    }
}
