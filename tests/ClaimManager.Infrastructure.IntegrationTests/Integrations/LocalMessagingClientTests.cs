using ClaimManager.Infrastructure.Integrations.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaimManager.Infrastructure.IntegrationTests.Integrations;

public sealed class LocalMessagingClientTests
{
    [Fact]
    public async Task SendAsync_returns_success_result_with_non_null_delivery_id()
    {
        var client = new LocalMessagingClient(NullLogger<LocalMessagingClient>.Instance);
        var message = new OutboundMessage("adjuster@example.com", "Claim update", "Body", "CLM-0001");

        var result = await client.SendAsync(message, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.DeliveryId);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task SendAsync_delivery_id_is_unique_per_call()
    {
        var client = new LocalMessagingClient(NullLogger<LocalMessagingClient>.Instance);
        var message = new OutboundMessage("adjuster@example.com", "Claim update", "Body", "CLM-0001");

        var firstResult = await client.SendAsync(message, CancellationToken.None);
        var secondResult = await client.SendAsync(message, CancellationToken.None);

        Assert.NotNull(firstResult.DeliveryId);
        Assert.NotNull(secondResult.DeliveryId);
        Assert.NotEqual(firstResult.DeliveryId, secondResult.DeliveryId);
    }
}