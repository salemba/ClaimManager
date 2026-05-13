namespace ClaimManager.Infrastructure.Integrations.PaymentSystem;

public interface IPaymentSystemClient
{
    Task<PaymentRecord?> GetPaymentStatusByClaimAsync(string claimNumber, CancellationToken cancellationToken);
}
