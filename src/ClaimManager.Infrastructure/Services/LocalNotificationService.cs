namespace ClaimManager.Infrastructure.Services;

using ClaimManager.Application.Interfaces;
using Microsoft.Extensions.Logging;

public class LocalNotificationService : INotificationService
{
    private readonly ILogger<LocalNotificationService> _logger;

    public LocalNotificationService(ILogger<LocalNotificationService> logger)
    {
        _logger = logger;
    }

    public Task NotifyAdjusterReassignmentAsync(string oldAdjusterId, string newAdjusterId, Guid claimId)
    {
        _logger.LogInformation($"Notifying adjusters about reassignment of claim {claimId}. Old: {oldAdjusterId}, New: {newAdjusterId}");
        // In a real implementation, this would send an email, push notification, etc.
        return Task.CompletedTask;
    }
}
