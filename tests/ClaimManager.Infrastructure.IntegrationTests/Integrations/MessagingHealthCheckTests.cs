using ClaimManager.Infrastructure.Integrations.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClaimManager.Infrastructure.IntegrationTests.Integrations;

public sealed class MessagingHealthCheckTests
{
    [Fact]
    public async Task HealthCheck_returns_Degraded_when_BaseUrl_is_not_configured()
    {
        var options = Options.Create(new MessagingOptions());
        var healthCheck = new MessagingHealthCheck(options, NullLogger<MessagingHealthCheck>.Instance);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("messaging", healthCheck, null, null)
        };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("no BaseUrl configured", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthCheck_returns_Degraded_when_BaseUrl_is_configured_but_only_stub_mode_is_available()
    {
        var options = Options.Create(new MessagingOptions { BaseUrl = "https://messaging.internal" });
        var healthCheck = new MessagingHealthCheck(options, NullLogger<MessagingHealthCheck>.Instance);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("messaging", healthCheck, null, null)
        };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("stub mode", result.Description, StringComparison.OrdinalIgnoreCase);
    }
}