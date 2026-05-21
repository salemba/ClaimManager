using System.Net;
using System.Net.Http.Json;
using ClaimManager.Api.Endpoints.Auth;
using ClaimManager.Application.Dashboard.Dtos;
using System.Text.Json;
using ClaimManager.Domain.Claims;
using ClaimManager.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task Supervisor_dashboard_returns_correct_workload_and_blocker_summaries()
    {
        // Arrange
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClaimManagerDbContext>();

        var now = DateTime.UtcNow;
        var agingCutoff = now.AddDays(-20);

        var user1 = "user1@claimmanager.local";
        var user2 = "user2@claimmanager.local";

        var claims = new List<Claim>
        {
            Claim.Create("C-001", "Claimant 1", "e1@ma.il", "123", "P-001", now.AddDays(-1), "type", "desc", user1, now.AddDays(-1)),
            Claim.Create("C-002", "Claimant 2", "e2@ma.il", "123", "P-002", agingCutoff, "type", "desc", user1, agingCutoff),
            Claim.Create("C-003", "Claimant 3", "e3@ma.il", "123", "P-003", now.AddDays(-2), "type", "desc", user2, now.AddDays(-2)),
            Claim.Create("C-004", "Claimant 4", "e4@ma.il", "123", "P-004", now.AddDays(-3), "type", "desc", user2, now.AddDays(-3)),
            Claim.Create("C-005", "Claimant 5", "e5@ma.il", "123", "P-005", agingCutoff, "type", "desc", user2, agingCutoff),
            Claim.Create("C-006", "Claimant 6", "e6@ma.il", "123", "P-006", now.AddDays(-5), "type", "desc", "creator", now.AddDays(-5)),
            Claim.Create("C-007", "Claimant 7", "e7@ma.il", "123", "P-007", now, "type", "desc", "creator", now)
        };

        // User1: 2 total, 1 stuck, 1 aging
        claims[0].OwnedByUserId = user1;
        claims[1].OwnedByUserId = user1;
        claims[1].BlockerType = "blocker-A";

        // User2: 3 total, 2 stuck, 1 aging
        claims[2].OwnedByUserId = user2;
        claims[3].OwnedByUserId = user2;
        claims[3].BlockerType = "blocker-B";
        claims[4].OwnedByUserId = user2;
        claims[4].BlockerType = "blocker-A";

        // Unassigned: 1 total, 1 stuck, not aging
        claims[5].OwnedByUserId = null;
        claims[5].BlockerType = "blocker-B";

        // Terminal status, should be ignored
        claims[6].Status = "closed";

        await dbContext.Claims.AddRangeAsync(claims);
        await dbContext.SaveChangesAsync();

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("supervisor@claimmanager.local", "Supervisor!2345"));

        // Act
        var response = await client.GetAsync("/api/supervisor-dashboard");
        var payload = await response.Content.ReadFromJsonAsync<SupervisorDashboardDto>();

        // Assert
        payload.Should().NotBeNull();

        payload!.WorkloadDistribution.Should().HaveCount(3);

        var user1Workload = payload.WorkloadDistribution.Single(w => w.OwnerId == user1);
        user1Workload.TotalCount.Should().Be(2);
        user1Workload.StuckCount.Should().Be(1);
        user1Workload.AgingCount.Should().Be(1);
        user1Workload.BlockerCount.Should().Be(1);

        var user2Workload = payload.WorkloadDistribution.Single(w => w.OwnerId == user2);
        user2Workload.TotalCount.Should().Be(3);
        user2Workload.StuckCount.Should().Be(2);
        user2Workload.AgingCount.Should().Be(1);
        user2Workload.BlockerCount.Should().Be(2);

        var unassignedWorkload = payload.WorkloadDistribution.Single(w => w.OwnerId == "unassigned");
        unassignedWorkload.TotalCount.Should().Be(1);
        unassignedWorkload.StuckCount.Should().Be(1);
        unassignedWorkload.AgingCount.Should().Be(0);
        unassignedWorkload.BlockerCount.Should().Be(1);

        payload.BlockerSummary.Should().HaveCount(2);

        var blockerA = payload.BlockerSummary.Single(b => b.BlockerType == "blocker-A");
        blockerA.Count.Should().Be(2);
        blockerA.AffectedOwnerCount.Should().Be(2);
        blockerA.AgingClaimCount.Should().Be(2);

        var blockerB = payload.BlockerSummary.Single(b => b.BlockerType == "blocker-B");
        blockerB.Count.Should().Be(2);
        blockerB.AffectedOwnerCount.Should().Be(1);
        blockerB.AgingClaimCount.Should().Be(0);
    }
}
