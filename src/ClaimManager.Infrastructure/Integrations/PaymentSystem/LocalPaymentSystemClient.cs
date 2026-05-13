using Microsoft.Extensions.Logging;

namespace ClaimManager.Infrastructure.Integrations.PaymentSystem;

public sealed class LocalPaymentSystemClient(ILogger<LocalPaymentSystemClient> logger) : IPaymentSystemClient
{
    public Task<PaymentRecord?> GetPaymentStatusByClaimAsync(string claimNumber, CancellationToken cancellationToken)
    {
        logger.LogDebug("LocalPaymentSystemClient is active — returning null (no payment on file) for {ClaimNumber}", claimNumber);
        return Task.FromResult<PaymentRecord?>(null);
    }
}
