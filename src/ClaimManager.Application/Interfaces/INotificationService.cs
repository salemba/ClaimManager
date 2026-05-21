namespace ClaimManager.Application.Interfaces;

public interface INotificationService
{
    Task NotifyAdjusterReassignmentAsync(string oldAdjusterId, string newAdjusterId, Guid claimId);
}
