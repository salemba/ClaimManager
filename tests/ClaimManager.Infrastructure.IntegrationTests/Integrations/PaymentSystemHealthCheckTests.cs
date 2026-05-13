using ClaimManager.Infrastructure.Integrations.PaymentSystem;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClaimManager.Infrastructure.IntegrationTests.Integrations;

public sealed class PaymentSystemHealthCheckTests
{
    [Fact]
    public async Task HealthCheck_returns_Degraded_when_BaseUrl_is_not_configured()
    {
        var options = Options.Create(new PaymentSystemOptions());
        var healthCheck = new PaymentSystemHealthCheck(options, NullLogger<PaymentSystemHealthCheck>.Instance);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("payment-system", healthCheck, null, null)
        };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("local stub mode", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthCheck_returns_Degraded_when_BaseUrl_is_configured_but_only_stub_mode_is_available()
    {
        var options = Options.Create(new PaymentSystemOptions { BaseUrl = "https://payment.internal" });
        var healthCheck = new PaymentSystemHealthCheck(options, NullLogger<PaymentSystemHealthCheck>.Instance);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("payment-system", healthCheck, null, null)
        };

        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("stub mode", result.Description, StringComparison.OrdinalIgnoreCase);
    }
}
