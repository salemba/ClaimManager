using System.Net;
using System.Net.Http.Json;
using ClaimManager.Api.Endpoints.Auth;
using ClaimManager.Application.Dashboard.Dtos;
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
}
