namespace ClaimManager.Infrastructure.Integrations.Messaging;

public sealed record OutboundMessage(
    string Recipient,
    string Subject,
    string Body,
    string? CorrelationId);