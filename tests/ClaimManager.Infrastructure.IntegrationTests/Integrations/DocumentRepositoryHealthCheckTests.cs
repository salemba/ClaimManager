using ClaimManager.Infrastructure.Integrations.DocumentRepository;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClaimManager.Infrastructure.IntegrationTests.Integrations;

public sealed class DocumentRepositoryHealthCheckTests
{
    [Fact]
    public async Task HealthCheck_returns_Degraded_when_BaseUrl_is_not_configured()
    {
        var options = Options.Create(new DocumentRepositoryOptions());
        var healthCheck = new DocumentRepositoryHealthCheck(options, NullLogger<DocumentRepositoryHealthCheck>.Instance);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("document-repository", healthCheck, null, null)
        };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("no BaseUrl configured", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthCheck_returns_Degraded_when_BaseUrl_is_configured_but_only_stub_mode_is_available()
    {
        var options = Options.Create(new DocumentRepositoryOptions { BaseUrl = "https://documents.internal" });
        var healthCheck = new DocumentRepositoryHealthCheck(options, NullLogger<DocumentRepositoryHealthCheck>.Instance);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("document-repository", healthCheck, null, null)
        };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("stub mode", result.Description, StringComparison.OrdinalIgnoreCase);
    }
}