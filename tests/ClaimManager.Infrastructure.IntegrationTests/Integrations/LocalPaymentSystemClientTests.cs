using ClaimManager.Infrastructure.Integrations.PaymentSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaimManager.Infrastructure.IntegrationTests.Integrations;

public sealed class LocalPaymentSystemClientTests
{
    [Fact]
    public async Task GetPaymentStatusByClaim_returns_null_for_any_input()
    {
        var client = new LocalPaymentSystemClient(NullLogger<LocalPaymentSystemClient>.Instance);

        var result = await client.GetPaymentStatusByClaimAsync("CLM-0001", CancellationToken.None);

        Assert.Null(result);
    }
}
