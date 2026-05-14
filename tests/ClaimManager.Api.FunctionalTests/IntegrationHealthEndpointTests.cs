using System.Net;
using System.Net.Http.Json;
using ClaimManager.Api.Endpoints.Auth;
using ClaimManager.Api.Endpoints.Workspace;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ClaimManager.Api.FunctionalTests;

[Collection("Functional")]
public sealed class IntegrationHealthEndpointTests(ClaimManagerApiFactory factory)
{
    [Fact]
    public async Task Unauthenticated_request_returns_401()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/workspace/integration-health");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_adjuster_receives_integration_health_response()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("adjuster@claimmanager.local", "Adjuster!2345"));

        var response = await client.GetAsync("/api/workspace/integration-health");
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceEndpoints.IntegrationHealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Entries);
        Assert.True(payload.ReportedAtUtc > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Integration_health_response_includes_all_four_boundaries()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("adjuster@claimmanager.local", "Adjuster!2345"));

        var response = await client.GetAsync("/api/workspace/integration-health");
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceEndpoints.IntegrationHealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);

        var names = payload!.Entries.Select(e => e.Name).ToHashSet();
        Assert.Contains("document-repository", names);
        Assert.Contains("messaging", names);
        Assert.Contains("payment-system", names);
        Assert.Contains("policy-system", names);
    }

    [Fact]
    public async Task All_integration_boundaries_are_degraded_in_local_stub_mode()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("adjuster@claimmanager.local", "Adjuster!2345"));

        var response = await client.GetAsync("/api/workspace/integration-health");
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceEndpoints.IntegrationHealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.All(payload!.Entries, e => Assert.Equal("degraded", e.Status));
    }

    [Fact]
    public async Task Each_entry_includes_stub_mode_description()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("adjuster@claimmanager.local", "Adjuster!2345"));

        var response = await client.GetAsync("/api/workspace/integration-health");
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceEndpoints.IntegrationHealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.All(payload!.Entries, e =>
        {
            Assert.NotEmpty(e.Description);
            Assert.Contains("stub mode", e.Description, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Recovery_preserves_recorded_incident_evidence_after_boundary_returns_healthy()
    {
        var state = new MutableIntegrationHealthState(HealthStatus.Degraded, "Policy system is running in local stub mode.");

        using var customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    options.Registrations.Clear();
                    options.Registrations.Add(new HealthCheckRegistration(
                        "policy-system",
                        _ => new MutableIntegrationHealthCheck(state),
                        null,
                        ["integration"]));
                });
            }));

        using var client = customFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("adjuster@claimmanager.local", "Adjuster!2345"));

        var degradedResponse = await client.GetAsync("/api/workspace/integration-health");
        var degradedPayload = await degradedResponse.Content.ReadFromJsonAsync<WorkspaceEndpoints.IntegrationHealthResponse>();

        Assert.Equal(HttpStatusCode.OK, degradedResponse.StatusCode);
        Assert.NotNull(degradedPayload);

        var degradedEntry = Assert.Single(degradedPayload!.Entries);
        Assert.Equal("degraded", degradedEntry.Status);
        Assert.NotNull(degradedEntry.ActiveIncidentStartedAtUtc);
        Assert.Null(degradedEntry.LastResolvedIncidentAtUtc);

        state.Set(HealthStatus.Healthy, "Policy system operational.");

        var recoveredResponse = await client.GetAsync("/api/workspace/integration-health");
        var recoveredPayload = await recoveredResponse.Content.ReadFromJsonAsync<WorkspaceEndpoints.IntegrationHealthResponse>();

        Assert.Equal(HttpStatusCode.OK, recoveredResponse.StatusCode);
        Assert.NotNull(recoveredPayload);

        var recoveredEntry = Assert.Single(recoveredPayload!.Entries);
        Assert.Equal("healthy", recoveredEntry.Status);
        Assert.Null(recoveredEntry.ActiveIncidentStartedAtUtc);
        Assert.NotNull(recoveredEntry.LastResolvedIncidentAtUtc);
    }

    private sealed class MutableIntegrationHealthState(HealthStatus status, string description)
    {
        public HealthStatus Status { get; private set; } = status;

        public string Description { get; private set; } = description;

        public void Set(HealthStatus status, string description)
        {
            Status = status;
            Description = description;
        }
    }

    private sealed class MutableIntegrationHealthCheck(MutableIntegrationHealthState state) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var result = state.Status switch
            {
                HealthStatus.Healthy => HealthCheckResult.Healthy(state.Description),
                HealthStatus.Unhealthy => HealthCheckResult.Unhealthy(state.Description),
                _ => HealthCheckResult.Degraded(state.Description),
            };

            return Task.FromResult(result);
        }
    }
}
