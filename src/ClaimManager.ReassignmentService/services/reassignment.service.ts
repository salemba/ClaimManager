import { Claim } from '../domain/claim';
import { ClaimIsClosedError, ClaimNotFoundError, ReassignmentConditionsNotMetError } from '../domain/errors';
import { IAuditRepository } from '../repositories/audit.repository';
import { IClaimRepository } from '../repositories/claim.repository';
import { canForceReassign } from '../use-cases/can-force-reassign';
import { INotificationService } from './notification.service';
import { WorkflowService } from './workflow.service';

export class ReassignmentService {
  constructor(
    private readonly claimRepository: IClaimRepository,
    private readonly auditRepository: IAuditRepository,
    private readonly notificationService: INotificationService,
    private readonly workflowService: WorkflowService,
    private readonly getCurrentDate: () => Date = () => new Date()
  ) {}

  public async forceReassignClaim(
    claimId: string,
    newAdjusterId: string,
    supervisorId: string
  ): Promise<Claim> {
    const claim = await this.claimRepository.findById(claimId);
    if (!claim) {
      throw new ClaimNotFoundError(claimId);
    }

    const { allowed, reasons } = canForceReassign(claim, this.getCurrentDate());
    if (!allowed) {
      throw new ReassignmentConditionsNotMetError(reasons);
    }

    const nextStatus = this.workflowService.getNextState(claim.status);

    const updatedClaim: Claim = {
      ...claim,
      adjusterId: newAdjusterId,
      status: nextStatus,
    };

    const persistedClaim = await this.claimRepository.update(updatedClaim);

    await this.auditRepository.save({
      supervisorId,
      timestamp: this.getCurrentDate(),
      reason: reasons.join('; '),
      beforeState: claim,
      afterState: persistedClaim,
      beforeAdjusterId: claim.adjusterId,
      afterAdjusterId: newAdjusterId,
    });

    // Non-blocking notifications
    try {
      await Promise.all([
        this.notificationService.notifyAdjuster(
          claim.adjusterId,
          `Claim ${claim.id} has been reassigned from you.`
        ),
        this.notificationService.notifyAdjuster(
          newAdjusterId,
          `Claim ${claim.id} has been assigned to you.`
        ),
      ]);
    } catch (error) {
      // Per requirements, notification failures should not block the process.
      // We should log this error to an observability platform.
      console.error('Failed to send notifications:', error);
    }

    return persistedClaim;
  }
}
