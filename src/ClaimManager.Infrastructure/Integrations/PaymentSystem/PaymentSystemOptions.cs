namespace ClaimManager.Infrastructure.Integrations.PaymentSystem;

public sealed class PaymentSystemOptions
{
    public string? BaseUrl { get; init; }
    public string? ApiKey { get; init; }
}
