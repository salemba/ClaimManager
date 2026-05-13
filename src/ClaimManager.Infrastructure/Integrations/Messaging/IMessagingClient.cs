namespace ClaimManager.Infrastructure.Integrations.Messaging;

public interface IMessagingClient
{
    Task<MessageDeliveryResult> SendAsync(OutboundMessage message, CancellationToken cancellationToken);
}