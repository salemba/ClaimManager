using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimManager.Infrastructure.Integrations.PolicySystem;

public sealed class PolicySystemHealthCheck(
    IOptions<PolicySystemOptions> options,
    ILogger<PolicySystemHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var message = string.IsNullOrWhiteSpace(options.Value.BaseUrl)
            ? "Policy system is running in local stub mode — no BaseUrl configured"
            : "Policy system is running in local stub mode — BaseUrl is configured but the live client is not implemented yet";

        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
        {
            logger.LogWarning(message);
            return Task.FromResult(HealthCheckResult.Degraded(message));
        }

        logger.LogWarning(message);
        return Task.FromResult(HealthCheckResult.Degraded(message));
    }
}
