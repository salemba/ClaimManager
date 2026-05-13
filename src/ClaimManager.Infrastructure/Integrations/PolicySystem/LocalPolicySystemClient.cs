using Microsoft.Extensions.Logging;

namespace ClaimManager.Infrastructure.Integrations.PolicySystem;

public sealed class LocalPolicySystemClient(ILogger<LocalPolicySystemClient> logger) : IPolicySystemClient
{
    public Task<PolicySummary?> GetPolicyByNumberAsync(string policyNumber, CancellationToken cancellationToken)
    {
        logger.LogDebug("LocalPolicySystemClient is active — returning hardcoded stub policy for {PolicyNumber}", policyNumber);

        var stub = new PolicySummary(
            PolicyNumber: "POL-0001",
            PolicyHolder: "Jane Doe",
            CoverageType: "Auto",
            EffectiveDate: new DateOnly(2025, 1, 1),
            ExpirationDate: new DateOnly(2026, 12, 31));

        return Task.FromResult<PolicySummary?>(stub);
    }
}
