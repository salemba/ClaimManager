using ClaimManager.Infrastructure.Integrations.PolicySystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaimManager.Infrastructure.IntegrationTests.Integrations;

public sealed class LocalPolicySystemClientTests
{
    [Fact]
    public async Task GetPolicyByNumber_returns_stub_summary_for_any_input()
    {
        var client = new LocalPolicySystemClient(NullLogger<LocalPolicySystemClient>.Instance);

        var result = await client.GetPolicyByNumberAsync("ANY-POLICY-NUMBER", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("POL-0001", result.PolicyNumber);
        Assert.Equal("Jane Doe", result.PolicyHolder);
        Assert.Equal("Auto", result.CoverageType);
        Assert.Equal(new DateOnly(2025, 1, 1), result.EffectiveDate);
        Assert.Equal(new DateOnly(2026, 12, 31), result.ExpirationDate);
    }
}
