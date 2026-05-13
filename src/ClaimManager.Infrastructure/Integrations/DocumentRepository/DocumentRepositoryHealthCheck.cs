using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimManager.Infrastructure.Integrations.DocumentRepository;

public sealed class DocumentRepositoryHealthCheck(
    IOptions<DocumentRepositoryOptions> options,
    ILogger<DocumentRepositoryHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var message = string.IsNullOrWhiteSpace(options.Value.BaseUrl)
            ? "Document repository is running in local stub mode — no BaseUrl configured"
            : "Document repository is running in local stub mode — BaseUrl is configured but the live client is not implemented yet";

        logger.LogWarning("{Message}", message);
        return Task.FromResult(HealthCheckResult.Degraded(message));
    }
}