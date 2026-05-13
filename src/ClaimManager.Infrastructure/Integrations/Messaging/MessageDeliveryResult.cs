namespace ClaimManager.Infrastructure.Integrations.Messaging;

public sealed record MessageDeliveryResult(
    bool Success,
    string? DeliveryId,
    string? FailureReason);