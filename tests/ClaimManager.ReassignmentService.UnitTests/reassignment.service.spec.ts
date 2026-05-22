import { Claim, ClaimStatus } from '../../src/ClaimManager.ReassignmentService/domain/claim';
import { ClaimIsClosedError, ClaimNotFoundError, ReassignmentConditionsNotMetError } from '../../src/ClaimManager.ReassignmentService/domain/errors';
import { IAuditRepository } from '../../src/ClaimManager.ReassignmentService/repositories/audit.repository';
import { IClaimRepository } from '../../src/ClaimManager.ReassignmentService/repositories/claim.repository';
import { INotificationService } from '../../src/ClaimManager.ReassignmentService/services/notification.service';
import { ReassignmentService } from '../../src/ClaimManager.ReassignmentService/services/reassignment.service';
import { WorkflowService } from '../../src/ClaimManager.ReassignmentService/services/workflow.service';

describe('ReassignmentService', () => {
  let claimRepository: jest.Mocked<IClaimRepository>;
  let auditRepository: jest.Mocked<IAuditRepository>;
  let notificationService: jest.Mocked<INotificationService>;
  let workflowService: jest.Mocked<WorkflowService>;
  let reassignmentService: ReassignmentService;

  const now = new Date();
  const getCurrentDate = () => now;

  const baseClaim: Claim = {
    id: 'claim-1',
    adjusterId: 'adjuster-1',
    status: ClaimStatus.UNDER_REVIEW,
    amount: 15000, // Meets amount condition
    blockedSince: null,
    createdAt: new Date(),
  };

  beforeEach(() => {
    claimRepository = {
      findById: jest.fn(),
      update: jest.fn(),
    };
    auditRepository = {
      save: jest.fn(),
    };
    notificationService = {
      notifyAdjuster: jest.fn(),
    };
    workflowService = new WorkflowService(); // Using real implementation for state transitions

    reassignmentService = new ReassignmentService(
      claimRepository,
      auditRepository,
      notificationService,
      workflowService,
      getCurrentDate
    );
  });

  it('should throw ClaimNotFoundError if claim is not found', async () => {
    claimRepository.findById.mockResolvedValue(null);
    await expect(
      reassignmentService.forceReassignClaim('claim-1', 'adjuster-2', 'supervisor-1')
    ).rejects.toThrow(ClaimNotFoundError);
  });

  it('should throw ReassignmentConditionsNotMetError if conditions are not met', async () => {
    const claim = { ...baseClaim, amount: 5000, blockedSince: now };
    claimRepository.findById.mockResolvedValue(claim);
    await expect(
      reassignmentService.forceReassignClaim('claim-1', 'adjuster-2', 'supervisor-1')
    ).rejects.toThrow(ReassignmentConditionsNotMetError);
  });
  
  it('should throw ClaimIsClosedError if claim status is CLOSED', async () => {
    const claim = { ...baseClaim, status: ClaimStatus.CLOSED };
    claimRepository.findById.mockResolvedValue(claim);

    await expect(
      reassignmentService.forceReassignClaim(claim.id, 'new-adjuster', 'supervisor-1')
    ).rejects.toThrow(new ClaimIsClosedError("Cannot determine next state for a closed claim."));
  });

  describe('when reassignment is allowed', () => {
    beforeEach(() => {
        const updatedClaim = { ...baseClaim, adjusterId: 'adjuster-2', status: ClaimStatus.PENDING_DOCS };
        claimRepository.findById.mockResolvedValue(baseClaim);
        claimRepository.update.mockResolvedValue(updatedClaim);
        notificationService.notifyAdjuster.mockResolvedValue(undefined);
    });

    it('should update adjusterId and status', async () => {
      await reassignmentService.forceReassignClaim('claim-1', 'adjuster-2', 'supervisor-1');
      
      const expectedNextState = workflowService.getNextState(baseClaim.status);
      
      expect(claimRepository.update).toHaveBeenCalledWith({
        ...baseClaim,
        adjusterId: 'adjuster-2',
        status: expectedNextState,
      });
    });

    it('should persist an audit entry', async () => {
        const expectedNextState = workflowService.getNextState(baseClaim.status);
        await reassignmentService.forceReassignClaim('claim-1', 'adjuster-2', 'supervisor-1');

        expect(auditRepository.save).toHaveBeenCalledWith({
            supervisorId: 'supervisor-1',
            timestamp: now,
            reason: 'Claim amount is over €10000.',
            beforeState: baseClaim,
            afterState: { ...baseClaim, adjusterId: 'adjuster-2', status: expectedNextState },
            beforeAdjusterId: 'adjuster-1',
            afterAdjusterId: 'adjuster-2',
        });
    });

    it('should notify both old and new adjusters', async () => {
        await reassignmentService.forceReassignClaim('claim-1', 'adjuster-2', 'supervisor-1');

        expect(notificationService.notifyAdjuster).toHaveBeenCalledWith(
            'adjuster-1',
            `Claim claim-1 has been reassigned from you.`
        );
        expect(notificationService.notifyAdjuster).toHaveBeenCalledWith(
            'adjuster-2',
            `Claim claim-1 has been assigned to you.`
        );
    });

    it('should succeed even if notifications fail', async () => {
        notificationService.notifyAdjuster.mockRejectedValue(new Error('Email service down'));
        
        await expect(reassignmentService.forceReassignClaim('claim-1', 'adjuster-2', 'supervisor-1')).resolves.toBeDefined();
        
        expect(claimRepository.update).toHaveBeenCalled();
        expect(auditRepository.save).toHaveBeenCalled();
      });
  });
});
