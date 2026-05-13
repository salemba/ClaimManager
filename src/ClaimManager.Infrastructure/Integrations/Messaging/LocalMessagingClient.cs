using Microsoft.Extensions.Logging;

namespace ClaimManager.Infrastructure.Integrations.Messaging;

public sealed class LocalMessagingClient(ILogger<LocalMessagingClient> logger) : IMessagingClient
{
    public Task<MessageDeliveryResult> SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var deliveryId = Guid.NewGuid().ToString("N");

        logger.LogInformation(
            "LocalMessagingClient is active — stub message sent to {Recipient} with subject {Subject} and correlation {CorrelationId}",
            message.Recipient,
            message.Subject,
            message.CorrelationId);

        return Task.FromResult(new MessageDeliveryResult(true, deliveryId, null));
    }
}