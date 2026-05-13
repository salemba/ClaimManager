namespace ClaimManager.Infrastructure.Integrations.PolicySystem;

public interface IPolicySystemClient
{
    Task<PolicySummary?> GetPolicyByNumberAsync(string policyNumber, CancellationToken cancellationToken);
}
