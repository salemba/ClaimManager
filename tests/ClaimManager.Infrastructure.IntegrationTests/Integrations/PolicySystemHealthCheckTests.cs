using ClaimManager.Infrastructure.Integrations.PolicySystem;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClaimManager.Infrastructure.IntegrationTests.Integrations;

public sealed class PolicySystemHealthCheckTests
{
    [Fact]
    public async Task HealthCheck_returns_Degraded_when_BaseUrl_is_not_configured()
    {
        var options = Options.Create(new PolicySystemOptions());
        var healthCheck = new PolicySystemHealthCheck(options, NullLogger<PolicySystemHealthCheck>.Instance);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("policy-system", healthCheck, null, null)
        };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("local stub mode", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthCheck_returns_Degraded_when_BaseUrl_is_configured_but_only_stub_mode_is_available()
    {
        var options = Options.Create(new PolicySystemOptions { BaseUrl = "https://policy.internal" });
        var healthCheck = new PolicySystemHealthCheck(options, NullLogger<PolicySystemHealthCheck>.Instance);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("policy-system", healthCheck, null, null)
        };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("stub mode", result.Description, StringComparison.OrdinalIgnoreCase);
    }
}
