using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimManager.Infrastructure.Integrations.Messaging;

public sealed class MessagingHealthCheck(
    IOptions<MessagingOptions> options,
    ILogger<MessagingHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var message = string.IsNullOrWhiteSpace(options.Value.BaseUrl)
            ? "Messaging is running in local stub mode — no BaseUrl configured"
            : "Messaging is running in local stub mode — BaseUrl is configured but the live client is not implemented yet";

        logger.LogWarning("{Message}", message);
        return Task.FromResult(HealthCheckResult.Degraded(message));
    }
}