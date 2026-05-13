namespace ClaimManager.Infrastructure.Integrations.PaymentSystem;

public sealed record PaymentRecord(
    string ClaimNumber,
    string PaymentReference,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset? SettledAt);
