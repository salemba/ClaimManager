using System.Net;
using System.Net.Http.Json;
using ClaimManager.Api.Endpoints.Auth;
using ClaimManager.Application.Dashboard.Dtos;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ClaimManager.Api.FunctionalTests;

[Collection("Functional")]
public sealed class SupervisorDashboardEndpointTests(ClaimManagerApiFactory factory)
{
    [Fact]
    public async Task Unauthenticated_request_returns_401()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/supervisor-dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Adjuster_access_is_rejected_with_403()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("adjuster@claimmanager.local", "Adjuster!2345"));

        var response = await client.GetAsync("/api/supervisor-dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Supervisor_receives_dashboard_response_with_aggregate_signals()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("supervisor@claimmanager.local", "Supervisor!2345"));

        var response = await client.GetAsync("/api/supervisor-dashboard");
        var payload = await response.Content.ReadFromJsonAsync<SupervisorDashboardDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.NotNull(payload!.Signals);
        Assert.True(payload.GeneratedAtUtc > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Admin_receives_dashboard_response()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("admin@claimmanager.local", "Admin!234567"));

        var response = await client.GetAsync("/api/supervisor-dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_response_shape_includes_signals_blocker_summary_and_preview_items()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("supervisor@claimmanager.local", "Supervisor!2345"));

        var response = await client.GetAsync("/api/supervisor-dashboard");
        var payload = await response.Content.ReadFromJsonAsync<SupervisorDashboardDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.NotNull(payload!.Signals);
        Assert.NotNull(payload.BlockerSummary);
        Assert.NotNull(payload.HighRiskClaims);
        Assert.NotNull(payload.AgingClaims);
    }

    [Fact]
    public async Task Dashboard_response_includes_workload_distribution_and_enriched_blocker_context()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("supervisor@claimmanager.local", "Supervisor!2345"));

        var response = await client.GetAsync("/api/supervisor-dashboard");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(document.RootElement.TryGetProperty("workloadDistribution", out var workloadDistribution));
        Assert.Equal(JsonValueKind.Array, workloadDistribution.ValueKind);

        Assert.True(document.RootElement.TryGetProperty("blockerSummary", out var blockerSummary));
        Assert.Equal(JsonValueKind.Array, blockerSummary.ValueKind);

        foreach (var blocker in blockerSummary.EnumerateArray())
        {
            Assert.True(blocker.TryGetProperty("affectedOwnerCount", out var affectedOwnerCount));
            Assert.True(blocker.TryGetProperty("agingClaimCount", out var agingClaimCount));
            Assert.InRange(affectedOwnerCount.GetInt32(), 0, int.MaxValue);
            Assert.InRange(agingClaimCount.GetInt32(), 0, int.MaxValue);
        }
    }

    [Fact]
    public async Task Dashboard_signals_are_non_negative()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("supervisor@claimmanager.local", "Supervisor!2345"));

        var response = await client.GetAsync("/api/supervisor-dashboard");
        var payload = await response.Content.ReadFromJsonAsync<SupervisorDashboardDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);

        var signals = payload!.Signals;
        Assert.True(signals.StuckCount >= 0);
        Assert.True(signals.AgingCount >= 0);
        Assert.True(signals.AttentionRequiredCount >= 0);
        Assert.True(signals.ApprovalPressureCount >= 0);
    }

    [Fact]
    public async Task High_risk_preview_only_contains_claims_with_blockers_or_data_integrity_warnings()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("supervisor@claimmanager.local", "Supervisor!2345"));

        var response = await client.GetAsync("/api/supervisor-dashboard");
        var payload = await response.Content.ReadFromJsonAsync<SupervisorDashboardDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.All(payload!.HighRiskClaims, claim =>
            Assert.True(claim.HasDataIntegrityWarning || !string.IsNullOrWhiteSpace(claim.BlockerType)));
    }

    [Fact]
    public async Task Dashboard_workload_and_blocker_summaries_have_consistent_data()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("supervisor@claimmanager.local", "Supervisor!2345"));

        var response = await client.GetAsync("/api/supervisor-dashboard");
        var payload = await response.Content.ReadFromJsonAsync<SupervisorDashboardDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);

        Assert.All(payload.WorkloadDistribution, w =>
        {
            Assert.False(string.IsNullOrWhiteSpace(w.OwnerId));
            Assert.True(w.TotalCount >= w.StuckCount);
            Assert.True(w.TotalCount >= w.AgingCount);
            Assert.True(w.StuckCount >= 0);
            Assert.True(w.AgingCount >= 0);
            Assert.True(w.BlockerCount >= 0);
        });

        if (payload.BlockerSummary.Any())
        {
            Assert.All(payload.BlockerSummary, b =>
            {
                Assert.False(string.IsNullOrWhiteSpace(b.BlockerType));
                Assert.True(b.Count > 0);
                Assert.True(b.AffectedOwnerCount >= 0);
                Assert.True(b.AgingClaimCount >= 0);
            });
        }
    }
}
