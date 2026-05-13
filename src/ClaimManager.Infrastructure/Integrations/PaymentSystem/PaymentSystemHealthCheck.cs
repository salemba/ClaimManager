using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimManager.Infrastructure.Integrations.PaymentSystem;

public sealed class PaymentSystemHealthCheck(
    IOptions<PaymentSystemOptions> options,
    ILogger<PaymentSystemHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var message = string.IsNullOrWhiteSpace(options.Value.BaseUrl)
            ? "Payment system is running in local stub mode — no BaseUrl configured"
            : "Payment system is running in local stub mode — BaseUrl is configured but the live client is not implemented yet";

        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
        {
            logger.LogWarning(message);
            return Task.FromResult(HealthCheckResult.Degraded(message));
        }

        logger.LogWarning(message);
        return Task.FromResult(HealthCheckResult.Degraded(message));
    }
}
