namespace ClaimManager.Infrastructure.Integrations.PolicySystem;

public sealed record PolicySummary(
    string PolicyNumber,
    string PolicyHolder,
    string CoverageType,
    DateOnly EffectiveDate,
    DateOnly ExpirationDate);
