namespace ClaimManager.Application.Services;

using ClaimManager.Application.Interfaces;
using ClaimManager.Domain.Claims.Entities;
using ClaimManager.Application.Exceptions;
using ClaimManager.Domain.Audit.Entities;

public class ClaimService
{
    private readonly IClaimRepository _claimRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly INotificationService _notificationService;

    public ClaimService(IClaimRepository claimRepository, IAuditRepository auditRepository, INotificationService notificationService)
    {
        _claimRepository = claimRepository;
        _auditRepository = auditRepository;
        _notificationService = notificationService;
    }

    public async Task<Claim> ForceReassignClaimAsync(Guid claimId, string newAdjusterId, string supervisorId)
    {
        var claim = await _claimRepository.GetByIdAsync(claimId);
        if (claim == null)
        {
            throw new ClaimNotFoundException(claimId);
        }

        var (allowed, reasons) = claim.CanForceReassign(DateTime.UtcNow);
        if (!allowed)
        {
            throw new ForceReassignmentNotAllowedException("Conditions for force reassignment are not met.");
        }

        var oldStatus = claim.Status;
        var oldAdjusterId = claim.AdjusterId;
        var newStatus = claim.GetNextStatus();

        var updatedClaim = claim.With(newAdjusterId, newStatus);

        await _claimRepository.UpdateAsync(updatedClaim);

        var audit = new ForceReassignAudit
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            SupervisorId = supervisorId,
            Timestamp = DateTime.UtcNow,
            Reasons = string.Join(", ", reasons),
            StatusBefore = oldStatus,
            StatusAfter = newStatus,
            AdjusterIdBefore = oldAdjusterId,
            AdjusterIdAfter = newAdjusterId
        };
        await _auditRepository.AddForceReassignAuditAsync(audit);

        try
        {
            await _notificationService.NotifyAdjusterReassignmentAsync(oldAdjusterId, newAdjusterId, claimId);
        }
        catch (Exception)
        {
            // Non-blocking as per requirements
        }

        return updatedClaim;
    }
}
